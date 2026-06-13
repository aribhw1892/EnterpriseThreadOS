using System.Text.Json;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Classification;
using ETOS.Backend.Documents;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernedQuery;

public interface IGovernedQueryService
{
    Task<RetrievalRunResponse> RunAsync(RunGovernedQueryRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RetrievalRunSummaryResponse>> ListRunsAsync(CancellationToken cancellationToken);
    Task<RetrievalRunResponse> GetRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<ContextPackageResponse> GetContextPackageAsync(Guid packageId, CancellationToken cancellationToken);
}

public sealed class GovernedQueryService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IGraphMemoryService graphMemoryService,
    IClassificationPolicyService classificationPolicyService,
    IAiTraceRecorder aiTraceRecorder) : IGovernedQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RetrievalRunResponse> RunAsync(RunGovernedQueryRequest request, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_query.run", GovernedQueryPermissions.Run, cancellationToken);
        var intentKey = NormalizeKey(request.IntentKey);
        var (intent, strategy) = await EnsureFixedIntentAsync(context, intentKey, cancellationToken);
        ValidateRunRequest(intent, request);

        var maxDepth = request.MaxDepth <= 0 ? 2 : Math.Min(request.MaxDepth, 4);
        var retrieved = await RetrieveCandidatesAsync(context.TenantId, intent, strategy, request, maxDepth, cancellationToken);
        var filtered = retrieved
            .Where(item => item.RequiredTrustState == TrustState.Trusted && item.GraphSpace is null or GraphSpace.Trusted)
            .Select((item, index) => item with { DisplayOrder = index })
            .ToList();

        var policyRequest = new EvaluatePolicyRequest(
            "governed_query.context_assembly",
            request.PolicyKey,
            filtered.Select(item => new PolicyEvaluationContextItem(
                item.ContextId,
                item.ContextType,
                item.ClassificationKey,
                item.AttributeKey,
                item.DocumentId,
                item.SafeSummary)).ToList());
        var policyEvaluation = await classificationPolicyService.EvaluateAsync(policyRequest, cancellationToken);
        var allowedIds = policyEvaluation.AllowedContext.Select(item => item.ContextId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowed = filtered
            .Where(item => allowedIds.Contains(item.ContextId))
            .Select((item, index) => item with { DisplayOrder = index })
            .ToList();
        var denied = policyEvaluation.DeniedSummaries
            .Select(summary => new DeniedContextSummaryResponse(summary.ContextId, summary.ContextType, summary.SafeSummary, summary.Reason))
            .ToList();
        var sensitiveDenied = policyEvaluation.SensitiveDeniedReferences
            .Select(reference => new SensitiveDeniedContextReferenceResponse(reference.ContextId, reference.ContextType, reference.DocumentId, reference.ClassificationKey, reference.AttributeKey, reference.Reason))
            .ToList();

        var run = new RetrievalRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            QueryIntentVersionId = intent.Id,
            RetrievalStrategyVersionId = strategy.Id,
            StartGraphNodeId = request.StartGraphNodeId,
            DocumentArtifactId = request.DocumentArtifactId,
            QueryText = TrimToMax(request.QueryText, 1000) ?? intent.Name,
            Status = "Completed",
            RetrievedCount = retrieved.Count,
            FilteredCount = filtered.Count,
            DeniedCount = denied.Count,
            SafeSummary = $"Governed query '{intent.IntentKey}' assembled {allowed.Count} LLM-safe context items and denied {denied.Count}.",
            RequestedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        var package = new ContextPackage
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            RetrievalRunId = run.Id,
            PolicyKey = request.PolicyKey,
            PolicyEvaluationId = policyEvaluation.EvaluationId,
            RetrievedContextJson = Serialize(retrieved.Select(ToContextItemResponse).ToList()),
            FilteredContextJson = Serialize(filtered.Select(ToContextItemResponse).ToList()),
            DeniedSummariesJson = Serialize(denied),
            SensitiveDeniedReferencesJson = Serialize(sensitiveDenied),
            LlmVisibleContextJson = Serialize(allowed.Select(ToContextItemResponse).ToList()),
            AllowedCount = allowed.Count,
            DeniedCount = denied.Count,
            SafeSummary = $"Context package contains {allowed.Count} LLM-visible safe summaries and {denied.Count} denied summaries.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var decisions = BuildAccessDecisions(context.TenantId, package.Id, allowed, denied);
        package.AccessDecisions.AddRange(decisions);
        run.ContextPackages.Add(package);
        dbContext.RetrievalRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "governed_query.run",
                AuditResult.Success,
                null,
                run.SafeSummary,
                nameof(RetrievalRun),
                run.Id.ToString(),
                request.PolicyKey,
                policyEvaluation.PolicyVersionLabel,
                RetentionCategory: AuditRetentionCategory.Operational,
                IsArchiveEligible: true),
            cancellationToken);
        run.AuditRecordId = audit.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (request.CreateAiTrace)
        {
            await aiTraceRecorder.CreateFromRetrievalRunAsync(run.Id, audit.Id, cancellationToken);
        }

        return await GetRunInternalAsync(run.Id, context, "governed_query.run.get_created", cancellationToken);
    }

    public async Task<IReadOnlyCollection<RetrievalRunSummaryResponse>> ListRunsAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_query.runs.list", GovernedQueryPermissions.Read, cancellationToken);
        var runs = await dbContext.RetrievalRuns
            .AsNoTracking()
            .Where(run => run.TenantId == context.TenantId)
            .Join(
                dbContext.QueryIntentVersions,
                run => run.QueryIntentVersionId,
                intent => intent.Id,
                (run, intent) => new { run, intent })
            .Join(
                dbContext.RetrievalStrategyVersions,
                pair => pair.run.RetrievalStrategyVersionId,
                strategy => strategy.Id,
                (pair, strategy) => new { pair.run, pair.intent, strategy })
            .OrderByDescending(pair => pair.run.CreatedAt)
            .Select(pair => new RetrievalRunSummaryResponse(
                pair.run.Id,
                pair.run.TenantId,
                pair.intent.IntentKey,
                pair.strategy.StrategyKey,
                pair.run.StartGraphNodeId,
                pair.run.DocumentArtifactId,
                pair.run.Status,
                pair.run.RetrievedCount,
                pair.run.FilteredCount,
                pair.run.DeniedCount,
                pair.run.SafeSummary,
                pair.run.RequestedByUserId,
                pair.run.CreatedAt,
                pair.run.CompletedAt))
            .ToListAsync(cancellationToken);

        return runs;
    }

    public async Task<RetrievalRunResponse> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_query.runs.get", GovernedQueryPermissions.Read, cancellationToken);
        return await GetRunInternalAsync(runId, context, "governed_query.runs.get", cancellationToken);
    }

    public async Task<ContextPackageResponse> GetContextPackageAsync(Guid packageId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_query.context_packages.get", GovernedQueryPermissions.Read, cancellationToken);
        var package = await dbContext.ContextPackages
            .AsNoTracking()
            .Include(item => item.AccessDecisions)
            .SingleOrDefaultAsync(item => item.Id == packageId, cancellationToken)
            ?? throw new RequestValidationException("Context package was not found.");
        if (package.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "governed_query.context_packages.get", "Context package belongs to a different tenant.", cancellationToken);
        }

        return ToContextPackageResponse(package);
    }

    private async Task<RetrievalRunResponse> GetRunInternalAsync(Guid runId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var run = await dbContext.RetrievalRuns
            .AsNoTracking()
            .Include(item => item.QueryIntentVersion)
            .Include(item => item.RetrievalStrategyVersion)
            .Include(item => item.ContextPackages)
                .ThenInclude(package => package.AccessDecisions)
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new RequestValidationException("Retrieval run was not found.");
        if (run.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, "Retrieval run belongs to a different tenant.", cancellationToken);
        }

        return ToRunResponse(run);
    }

    private async Task<(QueryIntentVersion Intent, RetrievalStrategyVersion Strategy)> EnsureFixedIntentAsync(
        ActiveTenantContext context,
        string normalizedIntentKey,
        CancellationToken cancellationToken)
    {
        var definition = FixedIntentDefinitions.SingleOrDefault(item => item.NormalizedIntentKey == normalizedIntentKey)
            ?? throw new RequestValidationException("Fixed query intent was not found.");
        var intent = await dbContext.QueryIntentVersions.SingleOrDefaultAsync(
            item => item.TenantId == context.TenantId && item.NormalizedIntentKey == definition.NormalizedIntentKey && item.Source == QueryIntentSource.PlatformFixed,
            cancellationToken);
        var strategy = await dbContext.RetrievalStrategyVersions.SingleOrDefaultAsync(
            item => item.TenantId == context.TenantId && item.NormalizedStrategyKey == definition.NormalizedStrategyKey && item.Source == QueryIntentSource.PlatformFixed,
            cancellationToken);

        if (intent is not null && strategy is not null)
        {
            await EnsureTenantPlaceholderAsync(context, cancellationToken);
            return (intent, strategy);
        }

        intent ??= new QueryIntentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            IntentKey = definition.IntentKey,
            NormalizedIntentKey = definition.NormalizedIntentKey,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            Name = definition.Name,
            Summary = definition.Summary,
            IntentKind = definition.Kind,
            Source = QueryIntentSource.PlatformFixed,
            IsEnabled = true,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        strategy ??= new RetrievalStrategyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            StrategyKey = definition.StrategyKey,
            NormalizedStrategyKey = definition.NormalizedStrategyKey,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            Name = $"{definition.Name} retrieval strategy",
            Summary = "Trusted graph first, linked document metadata second. Semantic/vector fallback deferred.",
            GraphSpace = GraphSpace.Trusted,
            RequiredTrustState = TrustState.Trusted,
            RelationshipTypesJson = Serialize(definition.RelationshipTypes),
            AllowsSemanticFallback = false,
            AllowsVectorFallback = false,
            Source = QueryIntentSource.PlatformFixed,
            IsEnabled = true,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (dbContext.Entry(intent).State == EntityState.Detached)
        {
            dbContext.QueryIntentVersions.Add(intent);
        }

        if (dbContext.Entry(strategy).State == EntityState.Detached)
        {
            dbContext.RetrievalStrategyVersions.Add(strategy);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureTenantPlaceholderAsync(context, cancellationToken);
        return (intent, strategy);
    }

    private async Task EnsureTenantPlaceholderAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        var exists = await dbContext.QueryIntentVersions.AnyAsync(
            item => item.TenantId == context.TenantId && item.Source == QueryIntentSource.TenantPlaceholder,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.QueryIntentVersions.Add(new QueryIntentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            IntentKey = "tenant-defined-placeholder",
            NormalizedIntentKey = "TENANT-DEFINED-PLACEHOLDER",
            VersionLabel = "future",
            NormalizedVersionLabel = "FUTURE",
            Name = "Tenant-defined query intents placeholder",
            Summary = "Tenant-defined query intents are deferred and cannot execute in Slice 13.",
            IntentKind = QueryIntentKind.Object360Context,
            Source = QueryIntentSource.TenantPlaceholder,
            IsEnabled = false,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<RetrievedContextCandidate>> RetrieveCandidatesAsync(
        Guid tenantId,
        QueryIntentVersion intent,
        RetrievalStrategyVersion strategy,
        RunGovernedQueryRequest request,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var candidates = new List<RetrievedContextCandidate>();
        if (request.StartGraphNodeId is not null)
        {
            var graph = await graphMemoryService.TraverseAsync(
                new TraverseGraphRequest(
                    tenantId,
                    request.StartGraphNodeId.Value,
                    GraphSpace.Trusted,
                    maxDepth,
                    Deserialize<IReadOnlyCollection<string>>(strategy.RelationshipTypesJson),
                    [TrustState.Trusted]),
                cancellationToken);
            candidates.AddRange(graph.Nodes.Select(node => FromGraphNode(node, candidates.Count)));
            candidates.AddRange(graph.Relationships.Select(relationship => FromGraphRelationship(relationship, candidates.Count)));
            var linkedDocuments = await LoadDocumentCandidatesForGraphNodesAsync(
                tenantId,
                graph.Nodes.Select(node => node.NodeId).Append(request.StartGraphNodeId.Value).Distinct().ToArray(),
                candidates.Count,
                cancellationToken);
            candidates.AddRange(linkedDocuments);
        }

        if (intent.IntentKind == QueryIntentKind.DocumentEvidenceContext && request.DocumentArtifactId is not null)
        {
            var documentCandidates = await LoadDocumentCandidatesAsync(tenantId, request.DocumentArtifactId.Value, candidates.Count, cancellationToken);
            candidates.AddRange(documentCandidates);
        }

        return candidates
            .Select((item, index) => item with { DisplayOrder = index })
            .ToList();
    }

    private async Task<IReadOnlyCollection<RetrievedContextCandidate>> LoadDocumentCandidatesForGraphNodesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> graphNodeIds,
        int startOrder,
        CancellationToken cancellationToken)
    {
        if (graphNodeIds.Count == 0)
        {
            return [];
        }

        var documents = await dbContext.DocumentObjectLinks
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.GraphNodeId != null && graphNodeIds.Contains(link.GraphNodeId.Value))
            .Join(
                dbContext.DocumentArtifacts,
                link => link.DocumentArtifactId,
                document => document.Id,
                (link, document) => new { link, document })
            .Join(
                dbContext.DocumentVersions,
                pair => pair.link.DocumentVersionId,
                version => version.Id,
                (pair, version) => new { pair.link, pair.document, version })
            .OrderByDescending(pair => pair.link.ConfidenceScore)
            .ThenByDescending(pair => pair.link.CreatedAt)
            .ToListAsync(cancellationToken);

        return documents
            .Select((pair, index) => FromDocument(pair.document, pair.version, pair.link, startOrder + index))
            .ToList();
    }

    private async Task<IReadOnlyCollection<RetrievedContextCandidate>> LoadDocumentCandidatesAsync(
        Guid tenantId,
        Guid documentArtifactId,
        int startOrder,
        CancellationToken cancellationToken)
    {
        var documents = await dbContext.DocumentArtifacts
            .AsNoTracking()
            .Where(document => document.TenantId == tenantId && document.Id == documentArtifactId)
            .Join(
                dbContext.DocumentVersions,
                document => document.Id,
                version => version.DocumentArtifactId,
                (document, version) => new { document, version })
            .GroupJoin(
                dbContext.DocumentObjectLinks,
                pair => pair.version.Id,
                link => link.DocumentVersionId,
                (pair, links) => new { pair.document, pair.version, link = links.OrderByDescending(item => item.CreatedAt).FirstOrDefault() })
            .OrderByDescending(pair => pair.version.CreatedAt)
            .ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            throw new RequestValidationException("Document artifact was not found for the active tenant.");
        }

        return documents
            .Select((pair, index) => FromDocument(pair.document, pair.version, pair.link, startOrder + index))
            .ToList();
    }

    private static RetrievedContextCandidate FromGraphNode(BaseNode node, int displayOrder)
    {
        var classification = AttributeValue(node.Attributes, "classificationKey") ?? AttributeValue(node.Attributes, "classification") ?? "internal";
        return new RetrievedContextCandidate(
            $"graph-node:{node.NodeId}",
            node.ObjectType,
            classification,
            null,
            null,
            "Graph",
            displayOrder,
            node.GraphSpace,
            node.TrustState,
            AttributeValue(node.Attributes, "safeSummary") ?? $"Trusted graph node '{node.ObjectType}' ({node.NodeId}).");
    }

    private static RetrievedContextCandidate FromGraphRelationship(BaseRelationship relationship, int displayOrder)
    {
        var classification = AttributeValue(relationship.Attributes, "classificationKey") ?? AttributeValue(relationship.Attributes, "classification") ?? "internal";
        return new RetrievedContextCandidate(
            $"graph-relationship:{relationship.RelationshipId}",
            relationship.RelationshipType,
            classification,
            null,
            null,
            "Graph",
            displayOrder,
            null,
            relationship.TrustState,
            AttributeValue(relationship.Attributes, "safeSummary") ?? $"Trusted graph relationship '{relationship.RelationshipType}' ({relationship.RelationshipId}).");
    }

    private static RetrievedContextCandidate FromDocument(DocumentArtifact document, DocumentVersion version, DocumentObjectLink? link, int displayOrder)
    {
        return new RetrievedContextCandidate(
            $"document:{document.Id}:version:{version.Id}",
            document.DocumentType,
            document.ClassificationKey,
            null,
            document.Id.ToString(),
            "Document",
            displayOrder,
            null,
            TrustState.Trusted,
            $"Document '{document.Title}' version '{version.VersionLabel}' metadata. Link evidence: {link?.EvidenceSummary ?? "direct document evidence"}");
    }

    private static List<ContextAccessDecision> BuildAccessDecisions(
        Guid tenantId,
        Guid packageId,
        IReadOnlyCollection<RetrievedContextCandidate> allowed,
        IReadOnlyCollection<DeniedContextSummaryResponse> denied)
    {
        var decisions = allowed
            .Select(item => new ContextAccessDecision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ContextPackageId = packageId,
                ContextId = item.ContextId,
                ContextType = item.ContextType,
                Result = ContextDecisionResult.Allowed,
                SafeSummary = item.SafeSummary,
                DisplayOrder = item.DisplayOrder,
                CreatedAt = DateTimeOffset.UtcNow
            })
            .ToList();
        decisions.AddRange(denied.Select((item, index) => new ContextAccessDecision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContextPackageId = packageId,
            ContextId = item.ContextId,
            ContextType = item.ContextType,
            Result = ContextDecisionResult.Denied,
            SafeSummary = item.SafeSummary,
            Reason = item.Reason,
            DisplayOrder = allowed.Count + index,
            CreatedAt = DateTimeOffset.UtcNow
        }));

        return decisions;
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, GovernedQueryPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "permission_denied", $"The user lacks the {permissionKey} permission.", cancellationToken);
            throw new TenantAccessDeniedException("User lacks governed query permission.");
        }

        return context;
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, string safeSummary, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("Governed query record is not available in the active tenant.");
    }

    private static void ValidateRunRequest(QueryIntentVersion intent, RunGovernedQueryRequest request)
    {
        if (intent.IntentKind is QueryIntentKind.Object360Context or QueryIntentKind.BomImpactContext && request.StartGraphNodeId is null)
        {
            throw new RequestValidationException("This query intent requires StartGraphNodeId.");
        }

        if (intent.IntentKind == QueryIntentKind.DocumentEvidenceContext && request.StartGraphNodeId is null && request.DocumentArtifactId is null)
        {
            throw new RequestValidationException("Document evidence context requires StartGraphNodeId or DocumentArtifactId.");
        }
    }

    private static RetrievalRunResponse ToRunResponse(RetrievalRun run)
    {
        return new RetrievalRunResponse(
            run.Id,
            run.TenantId,
            ToIntentResponse(run.QueryIntentVersion!),
            ToStrategyResponse(run.RetrievalStrategyVersion!),
            run.StartGraphNodeId,
            run.DocumentArtifactId,
            run.QueryText,
            run.Status,
            run.RetrievedCount,
            run.FilteredCount,
            run.DeniedCount,
            run.SafeSummary,
            run.RequestedByUserId,
            run.AuditRecordId,
            run.CreatedAt,
            run.CompletedAt,
            run.ContextPackages.OrderByDescending(package => package.CreatedAt).Select(ToContextPackageResponse).FirstOrDefault());
    }

    private static QueryIntentVersionResponse ToIntentResponse(QueryIntentVersion intent)
    {
        return new QueryIntentVersionResponse(intent.Id, intent.TenantId, intent.IntentKey, intent.VersionLabel, intent.Name, intent.Summary, intent.IntentKind, intent.Source, intent.IsEnabled, intent.CreatedAt);
    }

    private static RetrievalStrategyVersionResponse ToStrategyResponse(RetrievalStrategyVersion strategy)
    {
        return new RetrievalStrategyVersionResponse(
            strategy.Id,
            strategy.TenantId,
            strategy.StrategyKey,
            strategy.VersionLabel,
            strategy.Name,
            strategy.Summary,
            strategy.GraphSpace,
            strategy.RequiredTrustState,
            Deserialize<IReadOnlyCollection<string>>(strategy.RelationshipTypesJson),
            strategy.AllowsSemanticFallback,
            strategy.AllowsVectorFallback,
            strategy.Source,
            strategy.IsEnabled,
            strategy.CreatedAt);
    }

    private static ContextPackageResponse ToContextPackageResponse(ContextPackage package)
    {
        return new ContextPackageResponse(
            package.Id,
            package.TenantId,
            package.RetrievalRunId,
            package.PolicyKey,
            package.PolicyEvaluationId,
            Deserialize<IReadOnlyCollection<ContextItemResponse>>(package.RetrievedContextJson),
            Deserialize<IReadOnlyCollection<ContextItemResponse>>(package.FilteredContextJson),
            Deserialize<IReadOnlyCollection<ContextItemResponse>>(package.LlmVisibleContextJson),
            Deserialize<IReadOnlyCollection<DeniedContextSummaryResponse>>(package.DeniedSummariesJson),
            Deserialize<IReadOnlyCollection<SensitiveDeniedContextReferenceResponse>>(package.SensitiveDeniedReferencesJson),
            package.AccessDecisions.OrderBy(decision => decision.DisplayOrder).Select(ToDecisionResponse).ToList(),
            package.AllowedCount,
            package.DeniedCount,
            package.SafeSummary,
            package.CreatedAt);
    }

    private static ContextAccessDecisionResponse ToDecisionResponse(ContextAccessDecision decision)
    {
        return new ContextAccessDecisionResponse(decision.Id, decision.TenantId, decision.ContextPackageId, decision.ContextId, decision.ContextType, decision.Result, decision.SafeSummary, decision.Reason, decision.DisplayOrder, decision.CreatedAt);
    }

    private static ContextItemResponse ToContextItemResponse(RetrievedContextCandidate item)
    {
        return new ContextItemResponse(item.ContextId, item.ContextType, item.ClassificationKey, item.AttributeKey, item.DocumentId, item.SourceKind, item.DisplayOrder, item.SafeSummary);
    }

    private static string? AttributeValue(IReadOnlyDictionary<string, string?> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidOperationException("Stored governed query JSON could not be deserialized.");

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();

    private static string? TrimToMax(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed record RetrievedContextCandidate(
        string ContextId,
        string ContextType,
        string ClassificationKey,
        string? AttributeKey,
        string? DocumentId,
        string SourceKind,
        int DisplayOrder,
        GraphSpace? GraphSpace,
        TrustState RequiredTrustState,
        string SafeSummary);

    private sealed record FixedIntentDefinition(
        string IntentKey,
        string NormalizedIntentKey,
        string StrategyKey,
        string NormalizedStrategyKey,
        string Name,
        string Summary,
        QueryIntentKind Kind,
        IReadOnlyCollection<string> RelationshipTypes);

    private static readonly IReadOnlyCollection<FixedIntentDefinition> FixedIntentDefinitions =
    [
        new(
            "object-360-context",
            "OBJECT-360-CONTEXT",
            "object-360-trusted-graph-documents",
            "OBJECT-360-TRUSTED-GRAPH-DOCUMENTS",
            "Object 360 context",
            "Assembles trusted object neighborhood context with linked document evidence.",
            QueryIntentKind.Object360Context,
            ["RELATED_TO", "IDENTITY_LINK", "DOCUMENT_LINK", "HAS_VERSION", "PART_OF"]),
        new(
            "bom-impact-context",
            "BOM-IMPACT-CONTEXT",
            "bom-impact-trusted-graph-documents",
            "BOM-IMPACT-TRUSTED-GRAPH-DOCUMENTS",
            "BOM impact context",
            "Assembles trusted BOM relationship context with linked document evidence.",
            QueryIntentKind.BomImpactContext,
            ["BOM_CHILD", "BOM_PARENT", "USES", "PART_OF", "HAS_COMPONENT"]),
        new(
            "document-evidence-context",
            "DOCUMENT-EVIDENCE-CONTEXT",
            "document-evidence-trusted-graph-documents",
            "DOCUMENT-EVIDENCE-TRUSTED-GRAPH-DOCUMENTS",
            "Document evidence context",
            "Assembles trusted graph-linked document metadata evidence.",
            QueryIntentKind.DocumentEvidenceContext,
            ["DOCUMENT_LINK", "RELATED_TO", "PART_OF"])
    ];
}
