using System.Text.Json;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Explorers;

public interface IGraphExplorerService
{
    Task<IReadOnlyCollection<GraphExplorerNodeSummaryResponse>> ListNodesAsync(
        GraphSpace? graphSpace,
        TrustState? trustState,
        string? objectType,
        int? limit,
        string? policyKey,
        CancellationToken cancellationToken);

    Task<GraphExplorerNodeDetailResponse> GetNodeAsync(
        Guid nodeId,
        string? policyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GraphExplorerRelationshipResponse>> ListRelationshipsAsync(
        Guid nodeId,
        string? direction,
        string? policyKey,
        CancellationToken cancellationToken);
}

public sealed class GraphExplorerService(
    IGraphMemoryService graphMemoryService,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    ExplorerPolicyFilter policyFilter) : IGraphExplorerService
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    public async Task<IReadOnlyCollection<GraphExplorerNodeSummaryResponse>> ListNodesAsync(
        GraphSpace? graphSpace,
        TrustState? trustState,
        string? objectType,
        int? limit,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.list",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var resolvedSpace = graphSpace ?? GraphSpace.Trusted;
        var minimumTrust = trustState ?? (resolvedSpace == GraphSpace.Staging ? TrustState.Provisional : TrustState.Trusted);
        var graph = await graphMemoryService.ListGraphAsync(
            context.TenantId,
            resolvedSpace,
            null,
            null,
            null,
            cancellationToken);

        var filteredNodes = graph.Nodes
            .Where(node => ExplorerPolicyFilter.MeetsTrustFilter(node, minimumTrust))
            .Where(node => string.IsNullOrWhiteSpace(objectType)
                || string.Equals(node.ObjectType, objectType.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(NormalizeLimit(limit))
            .ToList();

        var responses = new List<GraphExplorerNodeSummaryResponse>();
        foreach (var node in filteredNodes)
        {
            var filtered = await policyFilter.FilterNodeAsync(node, policyKey, cancellationToken);
            responses.Add(new GraphExplorerNodeSummaryResponse(
                node.NodeId,
                node.ObjectType,
                node.TrustState.ToString(),
                node.GraphSpace.ToString(),
                filtered.SafeSummary,
                node.SourceReference?.SourceBatchId,
                filtered.AllowedAttributes));
        }

        return responses;
    }

    public async Task<GraphExplorerNodeDetailResponse> GetNodeAsync(
        Guid nodeId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.get",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var node = await graphMemoryService.GetNodeAsync(context.TenantId, nodeId, cancellationToken)
            ?? throw new RequestValidationException("Graph node was not found.");

        var filtered = await policyFilter.FilterNodeAsync(node, policyKey, cancellationToken);
        return new GraphExplorerNodeDetailResponse(
            node.NodeId,
            node.ObjectType,
            node.TrustState.ToString(),
            node.GraphSpace.ToString(),
            filtered.SafeSummary,
            node.SourceReference?.SourceBatchId,
            filtered.AllowedAttributes,
            $"/graph/{node.NodeId}",
            $"/chat?startGraphNodeId={node.NodeId}");
    }

    public async Task<IReadOnlyCollection<GraphExplorerRelationshipResponse>> ListRelationshipsAsync(
        Guid nodeId,
        string? direction,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.relationships",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var traversal = await graphMemoryService.TraverseAsync(
            new TraverseGraphRequest(
                context.TenantId,
                nodeId,
                null,
                1,
                null,
                [TrustState.Trusted, TrustState.Provisional]),
            cancellationToken);

        var normalizedDirection = string.IsNullOrWhiteSpace(direction) ? "both" : direction.Trim().ToLowerInvariant();
        var responses = new List<GraphExplorerRelationshipResponse>();

        foreach (var relationship in traversal.Relationships)
        {
            var isOutgoing = relationship.FromNodeId == nodeId;
            var isIncoming = relationship.ToNodeId == nodeId;
            if (normalizedDirection == "out" && !isOutgoing)
            {
                continue;
            }

            if (normalizedDirection == "in" && !isIncoming)
            {
                continue;
            }

            var adjacentNodeId = isOutgoing ? relationship.ToNodeId : relationship.FromNodeId;
            var adjacentNode = traversal.Nodes.SingleOrDefault(node => node.NodeId == adjacentNodeId);
            if (adjacentNode is null)
            {
                continue;
            }

            var filtered = await policyFilter.FilterNodeAsync(adjacentNode, policyKey, cancellationToken);
            responses.Add(new GraphExplorerRelationshipResponse(
                relationship.RelationshipId,
                relationship.RelationshipType,
                isOutgoing ? "out" : "in",
                adjacentNodeId,
                adjacentNode.ObjectType,
                adjacentNode.TrustState.ToString(),
                filtered.SafeSummary));
        }

        return responses;
    }

    private static int NormalizeLimit(int? limit)
    {
        if (limit is null or <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks graph explorer permission.");
        }

        return context;
    }
}

public interface IContextPackageExplorerService
{
    Task<IReadOnlyCollection<ContextPackageExplorerSummaryResponse>> ListPackagesAsync(CancellationToken cancellationToken);
    Task<ContextPackageExplorerDetailResponse> GetPackageAsync(Guid packageId, CancellationToken cancellationToken);
}

public sealed class ContextPackageExplorerService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IGovernedQueryService governedQueryService) : IContextPackageExplorerService
{
    public async Task<IReadOnlyCollection<ContextPackageExplorerSummaryResponse>> ListPackagesAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionsAsync(
            "explorers.context_packages.list",
            [ExplorerPermissions.Read, GovernedQueryPermissions.Read],
            cancellationToken);

        var runs = await governedQueryService.ListRunsAsync(cancellationToken);
        var summaries = new List<ContextPackageExplorerSummaryResponse>();
        foreach (var run in runs.OrderByDescending(item => item.CreatedAt).Take(50))
        {
            var packageId = await dbContext.ContextPackages
                .AsNoTracking()
                .Where(package => package.TenantId == context.TenantId && package.RetrievalRunId == run.Id)
                .Select(package => package.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (packageId == Guid.Empty)
            {
                continue;
            }

            Guid? traceId = null;
            if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Read, cancellationToken))
            {
                traceId = await dbContext.AiTraceRecords
                    .AsNoTracking()
                    .Where(trace => trace.TenantId == context.TenantId && trace.RetrievalRunId == run.Id)
                    .Select(trace => (Guid?)trace.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            summaries.Add(new ContextPackageExplorerSummaryResponse(
                packageId,
                run.Id,
                run.IntentKey,
                run.StrategyKey,
                run.RetrievedCount,
                run.FilteredCount,
                run.DeniedCount,
                run.SafeSummary,
                run.CreatedAt,
                traceId));
        }

        return summaries;
    }

    public async Task<ContextPackageExplorerDetailResponse> GetPackageAsync(Guid packageId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionsAsync(
            "explorers.context_packages.get",
            [ExplorerPermissions.Read, GovernedQueryPermissions.Read],
            cancellationToken);

        var package = await governedQueryService.GetContextPackageAsync(packageId, cancellationToken);
        var run = await dbContext.RetrievalRuns
            .AsNoTracking()
            .Join(
                dbContext.QueryIntentVersions,
                item => item.QueryIntentVersionId,
                intent => intent.Id,
                (item, intent) => new { item, intent })
            .Join(
                dbContext.RetrievalStrategyVersions,
                pair => pair.item.RetrievalStrategyVersionId,
                strategy => strategy.Id,
                (pair, strategy) => new
                {
                    pair.item,
                    pair.intent,
                    strategy
                })
            .SingleOrDefaultAsync(pair => pair.item.Id == package.RetrievalRunId && pair.item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Retrieval run was not found.");

        Guid? traceId = null;
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Read, cancellationToken))
        {
            traceId = await dbContext.AiTraceRecords
                .AsNoTracking()
                .Where(trace => trace.TenantId == context.TenantId && trace.RetrievalRunId == run.item.Id)
                .Select(trace => (Guid?)trace.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new ContextPackageExplorerDetailResponse(
            package.Id,
            package.RetrievalRunId,
            run.intent.IntentKey,
            run.strategy.StrategyKey,
            package.AllowedCount,
            package.DeniedCount,
            package.SafeSummary,
            traceId,
            traceId.HasValue ? $"/ai-traces/{traceId}" : null,
            package.DeniedSummaries.Select(item => item.SafeSummary).Take(5).ToList());
    }

    private async Task<ActiveTenantContext> RequirePermissionsAsync(
        string action,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        foreach (var permissionKey in permissionKeys)
        {
            if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
            {
                await denialRecorder.RecordAsync(
                    context.TenantId,
                    context.UserId,
                    action,
                    "permission_denied",
                    $"The user lacks the {permissionKey} permission.",
                    cancellationToken);
                throw new TenantAccessDeniedException($"User lacks {permissionKey} permission.");
            }
        }

        return context;
    }
}

public interface IDecisionExplorerFoundationService
{
    Task<IReadOnlyCollection<DecisionExplorerItemResponse>> ListDecisionsAsync(
        string? status,
        string? participant,
        string? search,
        CancellationToken cancellationToken);
}

public interface IArtifactExplorerService
{
    Task<IReadOnlyCollection<ArtifactExplorerSummaryResponse>> ListArtifactsAsync(
        string? artifactType,
        string? lifecycleState,
        string? search,
        CancellationToken cancellationToken);
}

public sealed class DecisionExplorerFoundationService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IDecisionExplorerFoundationService
{
    private static readonly HashSet<string> DecisionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DECISION",
        "DECISION-ARTIFACT"
    };

    public async Task<IReadOnlyCollection<DecisionExplorerItemResponse>> ListDecisionsAsync(
        string? status,
        string? participant,
        string? search,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.decisions.list",
            ExplorerPermissions.Read,
            cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId && DecisionTypes.Contains(artifact.NormalizedArtifactType))
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        var responses = new List<DecisionExplorerItemResponse>();
        foreach (var artifact in artifacts)
        {
            versionLookup.TryGetValue(artifact.Id, out var version);
            var payload = ParseDecisionPayload(version?.PayloadJson);
            var itemStatus = payload.Status ?? "draft";
            if (!string.IsNullOrWhiteSpace(status)
                && !string.Equals(itemStatus, status.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(participant)
                && !payload.ParticipantUserIds.Any(id => string.Equals(id, participant.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var title = payload.Title ?? artifact.Name;
            if (!string.IsNullOrWhiteSpace(search)
                && title.Contains(search, StringComparison.OrdinalIgnoreCase) == false
                && artifact.Name.Contains(search, StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            responses.Add(new DecisionExplorerItemResponse(
                artifact.Id,
                artifact.ArtifactType,
                title,
                itemStatus,
                payload.ParticipantUserIds,
                payload.EvidenceCount,
                payload.ConflictState ?? "unknown",
                payload.OutcomeSummary ?? "Outcome not recorded.",
                $"/artifacts/{artifact.Id}"));
        }

        return responses;
    }

    private static DecisionPayload ParseDecisionPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return DecisionPayload.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<DecisionPayload>(payloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? DecisionPayload.Empty;
        }
        catch (JsonException)
        {
            return DecisionPayload.Empty;
        }
    }

    private sealed record DecisionPayload
    {
        public static DecisionPayload Empty { get; } = new();

        public string? Title { get; init; }
        public string? Status { get; init; }
        public IReadOnlyCollection<string> ParticipantUserIds { get; init; } = [];
        public int EvidenceCount { get; init; }
        public string? ConflictState { get; init; }
        public string? OutcomeSummary { get; init; }
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks explorers permission.");
        }

        return context;
    }
}

public sealed class ArtifactExplorerService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IArtifactExplorerService
{
    public async Task<IReadOnlyCollection<ArtifactExplorerSummaryResponse>> ListArtifactsAsync(
        string? artifactType,
        string? lifecycleState,
        string? search,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionsAsync(
            "explorers.artifacts.list",
            [ExplorerPermissions.Read, ArtifactPermissions.Read],
            cancellationToken);

        var query = dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId);

        if (!string.IsNullOrWhiteSpace(artifactType))
        {
            var normalizedType = artifactType.Trim().ToUpperInvariant();
            query = query.Where(artifact => artifact.NormalizedArtifactType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(lifecycleState)
            && Enum.TryParse<ArtifactLifecycleState>(lifecycleState, true, out var parsedLifecycle))
        {
            query = query.Where(artifact => artifact.LifecycleState == parsedLifecycle);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(artifact =>
                artifact.Name.Contains(trimmedSearch)
                || (artifact.Description != null && artifact.Description.Contains(trimmedSearch)));
        }

        var artifacts = await query
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        return artifacts.Select(artifact =>
        {
            versionLookup.TryGetValue(artifact.Id, out var version);
            return new ArtifactExplorerSummaryResponse(
                artifact.Id,
                artifact.ArtifactType,
                artifact.Name,
                artifact.LifecycleState.ToString(),
                version?.VersionLabel,
                artifact.Description ?? $"Artifact '{artifact.Name}'.",
                $"/artifacts/{artifact.Id}",
                artifact.UpdatedAt);
        }).ToList();
    }

    private async Task<ActiveTenantContext> RequirePermissionsAsync(
        string action,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        foreach (var permissionKey in permissionKeys)
        {
            if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
            {
                await denialRecorder.RecordAsync(
                    context.TenantId,
                    context.UserId,
                    action,
                    "permission_denied",
                    $"The user lacks the {permissionKey} permission.",
                    cancellationToken);
                throw new TenantAccessDeniedException($"User lacks {permissionKey} permission.");
            }
        }

        return context;
    }
}
