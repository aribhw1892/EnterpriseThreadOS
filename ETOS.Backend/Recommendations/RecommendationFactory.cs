using System.Text.Json;
using ETOS.Backend.Agents;
using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Recommendations;

public interface IRecommendationFactory
{
    Task<CreateRecommendationResponse> FromDataQualityIssueAsync(Guid issueId, CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> FromBomComparisonRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> FromBomComparisonRunForImportAsync(
        Guid runId,
        ActiveTenantContext context,
        CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> FromBomComparisonRunAsync(
        Guid runId,
        Guid? dashboardArtifactId,
        Guid? reportArtifactId,
        CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> FromChatDraftAsync(
        ActiveTenantContext context,
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> FromAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> FromWorkflowRunAsync(Guid workflowRunId, CancellationToken cancellationToken);
}

public sealed class RecommendationFactory(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IModelPackageContextResolver modelPackageContextResolver) : IRecommendationFactory
{
    public async Task<CreateRecommendationResponse> FromDataQualityIssueAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var issue = await dbContext.DataQualityIssues
            .SingleOrDefaultAsync(item => item.Id == issueId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Data quality issue was not found.");

        var uniqueSourceKey = $"dq:{issue.Id}:DATA_QUALITY";
        var existing = await FindByUniqueSourceKeyAsync(context.TenantId, uniqueSourceKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var trustState = issue.ExcludedFromTrustedRecommendations
            ? TrustState.Conflicted
            : issue.ResultingTrustState;

        var evidence = new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
            Guid.NewGuid(),
            EvidenceLinkType.DataQualityIssue,
            issue.Id,
            issue.EvidenceSummary,
            trustState,
            false);

        var actions = new[]
        {
            new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                Guid.NewGuid(),
                $"Review data quality issue {issue.IssueCode}",
                "REVIEW_DATA_QUALITY",
                MapSeverityToRisk(issue.Severity),
                "DATA_STEWARD_REVIEW",
                SuggestedActionStatus.Proposed,
                issue.Rationale)
        };

        var relatedObjects = issue.GraphNodeId is null
            ? Array.Empty<RecommendationPayloadParser.RecommendationRelatedObjectDocument>()
            : [new RecommendationPayloadParser.RecommendationRelatedObjectDocument(issue.GraphNodeId, issue.AffectedEntityType.ToString())];

        return await CreateArtifactAsync(
            context,
            $"Data quality: {issue.Title}",
            issue.EvidenceSummary,
            RecommendationType.DataQuality,
            RecommendationCreationSource.DataQuality,
            MapSeverityToRisk(issue.Severity),
            RecommendationCapabilityState.ReviewRequired,
            [evidence],
            actions,
            relatedObjects,
            null,
            new RecommendationPayloadParser.RecommendationSourceReferenceDocument("data_quality_issue", issue.Id),
            uniqueSourceKey,
            cancellationToken);
    }

    public Task<CreateRecommendationResponse> FromBomComparisonRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        return FromBomComparisonRunInternalAsync(null, runId, null, null, cancellationToken);
    }

    public Task<CreateRecommendationResponse> FromBomComparisonRunForImportAsync(
        Guid runId,
        ActiveTenantContext context,
        CancellationToken cancellationToken)
        => FromBomComparisonRunInternalAsync(context, runId, null, null, cancellationToken);

    public Task<CreateRecommendationResponse> FromBomComparisonRunAsync(
        Guid runId,
        Guid? dashboardArtifactId,
        Guid? reportArtifactId,
        CancellationToken cancellationToken)
        => FromBomComparisonRunInternalAsync(null, runId, dashboardArtifactId, reportArtifactId, cancellationToken);

    private async Task<CreateRecommendationResponse> FromBomComparisonRunInternalAsync(
        ActiveTenantContext? callerContext,
        Guid runId,
        Guid? dashboardArtifactId,
        Guid? reportArtifactId,
        CancellationToken cancellationToken)
    {
        var context = callerContext ?? await RequireCreatePermissionAsync(cancellationToken);
        var run = await dbContext.BomComparisonRuns
            .SingleOrDefaultAsync(item => item.Id == runId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("BOM comparison run was not found.");

        var driftCount = run.MissingInSecondarySideCount + run.QuantityMismatchCount + run.UsageReferenceMismatchCount;
        if (driftCount == 0 && run.MissingInPrimarySideCount == 0)
        {
            throw new RequestValidationException("BOM comparison run has no drift requiring a recommendation.");
        }

        var uniqueSourceKey = $"bom:{run.Id}:BOM_SYNC";
        var existing = await FindByUniqueSourceKeyAsync(context.TenantId, uniqueSourceKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var templates = await LoadRecommendationTemplatesAsync(context, run.ImportBatchId, cancellationToken);

        var summary = FormatTemplate(
            templates?.StructuralDriftSummary,
            "Structural drift detected. Missing in secondary {missingInSecondary}, missing in primary {missingInPrimary}, quantity mismatches {quantityMismatches}.",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["missingInSecondary"] = run.MissingInSecondarySideCount.ToString(),
                ["missingInPrimary"] = run.MissingInPrimarySideCount.ToString(),
                ["quantityMismatches"] = run.QuantityMismatchCount.ToString()
            });

        var evidence = new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>
        {
            new(
                Guid.NewGuid(),
                EvidenceLinkType.BomComparisonRun,
                run.Id,
                summary,
                run.UnresolvedIdentityCount > 0 ? TrustState.Provisional : TrustState.Trusted,
                false),
            new(
                Guid.NewGuid(),
                EvidenceLinkType.ImportBatch,
                run.ImportBatchId,
                $"Import batch evidence for BOM comparison run {run.Id:N}.",
                TrustState.Trusted,
                false)
        };

        if (dashboardArtifactId is not null)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.Dashboard,
                dashboardArtifactId.Value,
                "Recommendation created from dashboard BOM investigation.",
                TrustState.Trusted,
                false));
        }

        if (reportArtifactId is not null)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.Report,
                reportArtifactId.Value,
                "Recommendation created from report BOM investigation.",
                TrustState.Trusted,
                false));
        }

        var actions = new[]
        {
            new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                Guid.NewGuid(),
                templates?.ReviewPrimarySideActionTitle ?? "Review secondary side synchronization",
                templates?.ReviewPrimarySideActionCode ?? "REVIEW_STRUCTURAL_SECONDARY",
                run.UnresolvedIdentityCount > 0 ? RecommendationRiskState.High : RecommendationRiskState.Medium,
                "ENGINEERING_REVIEW",
                SuggestedActionStatus.Proposed,
                templates?.ReviewPrimarySideActionRationale ?? "Validate whether the secondary structural side should be updated."),
            new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                Guid.NewGuid(),
                templates?.ReviewImpactActionTitle ?? "Review downstream impact",
                templates?.ReviewImpactActionCode ?? "REVIEW_STRUCTURAL_IMPACT",
                RecommendationRiskState.High,
                "DOMAIN_REVIEW",
                SuggestedActionStatus.Proposed,
                templates?.ReviewImpactActionRationale ?? "Assess downstream impact from structural drift.")
        };

        var response = await CreateArtifactAsync(
            context,
            templates?.StructuralDriftTitle ?? "Review structural synchronization",
            summary,
            RecommendationType.BomSync,
            dashboardArtifactId is not null
                ? RecommendationCreationSource.Dashboard
                : reportArtifactId is not null
                    ? RecommendationCreationSource.Report
                    : RecommendationCreationSource.BomComparison,
            run.UnresolvedIdentityCount > 0 ? RecommendationRiskState.High : RecommendationRiskState.Medium,
            RecommendationCapabilityState.ReadOnlyAnalysis,
            evidence,
            actions,
            [],
            null,
            new RecommendationPayloadParser.RecommendationSourceReferenceDocument("bom_comparison_run", run.Id),
            uniqueSourceKey,
            cancellationToken);

        if (dashboardArtifactId is not null)
        {
            await AddRelationshipAsync(context.TenantId, response.ArtifactId, dashboardArtifactId.Value, "Created from dashboard BOM investigation.", cancellationToken);
        }

        if (reportArtifactId is not null)
        {
            await AddRelationshipAsync(context.TenantId, response.ArtifactId, reportArtifactId.Value, "Created from report BOM investigation.", cancellationToken);
        }

        return response;
    }

    public async Task<CreateRecommendationResponse> FromChatDraftAsync(
        ActiveTenantContext context,
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Chat draft artifact was not found.");

        if (!artifact.ArtifactType.Equals(RecommendationArtifactTypes.Recommendation, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a recommendation draft.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Recommendation draft version was not found.");

        var payload = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        payload.CreationSource = RecommendationCreationSource.Chat;
        payload.CreatedFromChat = true;

        if (payload.EvidenceLinks.Count == 0 && payload.Explainability?.AiTraceId is Guid traceId)
        {
            payload.EvidenceLinks.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.AiTrace,
                traceId,
                "Governed chat turn AI trace evidence.",
                TrustState.Provisional,
                false));
        }

        version.PayloadJson = RecommendationPayloadParser.Serialize(payload);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateRecommendationResponse(
            artifact.Id,
            version.Id,
            version.VersionLabel,
            payload.LifecycleStatus);
    }

    public async Task<CreateRecommendationResponse> FromAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var agentRun = await dbContext.AgentRuns
            .SingleOrDefaultAsync(item => item.Id == agentRunId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Agent run was not found.");

        if (string.IsNullOrWhiteSpace(agentRun.StructuredOutputJson))
        {
            throw new RequestValidationException("Agent run does not contain structured output for recommendation creation.");
        }

        var agentVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == agentRun.AgentVersionId, cancellationToken)
            ?? throw new RequestValidationException("Agent version was not found.");

        var outputSchemaPayload = await LoadOutputSchemaPayloadAsync(context.TenantId, agentVersion.PayloadJson, cancellationToken);
        if (OutputSchemaCreatesDecision(outputSchemaPayload))
        {
            throw new RequestValidationException("Agent output schema must not create decision artifacts.");
        }

        GuardStructuredOutputAgainstDecisionCreation(agentRun.StructuredOutputJson);

        var uniqueSourceKey = $"agent:{agentRun.Id}";
        var existing = await FindByUniqueSourceKeyAsync(context.TenantId, uniqueSourceKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        using var outputDocument = JsonDocument.Parse(agentRun.StructuredOutputJson);
        var title = ReadStringProperty(outputDocument.RootElement, "title", "displayName", "name")
            ?? $"Agent recommendation: {agentVersion.Artifact?.Name ?? agentRun.Id.ToString()}";
        var summary = ReadStringProperty(outputDocument.RootElement, "summary", "answer", "rationale", "recommendation")
            ?? agentRun.OutputSafeSummaryJson
            ?? "Recommendation created from governed agent execution.";

        var evidence = new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>
        {
            new(
                Guid.NewGuid(),
                EvidenceLinkType.AgentRun,
                agentRun.Id,
                agentRun.OutputSafeSummaryJson ?? summary,
                TrustState.Provisional,
                false)
        };

        if (agentRun.RetrievalRunId is Guid retrievalRunId)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.RetrievalRun,
                retrievalRunId,
                "Governed retrieval evidence from agent execution.",
                TrustState.Provisional,
                false));
        }

        if (agentRun.AiTraceRecordId is Guid aiTraceId)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.AiTrace,
                aiTraceId,
                "AI trace evidence from agent execution.",
                TrustState.Provisional,
                false));
        }

        var childToolRuns = await dbContext.ToolRuns
            .AsNoTracking()
            .Where(item => item.ParentAgentRunId == agentRun.Id && item.TenantId == context.TenantId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var toolRun in childToolRuns)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.ToolRun,
                toolRun.Id,
                toolRun.OutputSafeSummaryJson ?? $"Tool run {toolRun.Status}.",
                TrustState.Provisional,
                false));
        }

        var suggestedActions = BuildSuggestedActionsFromOutput(outputDocument.RootElement, summary);
        var explainability = new RecommendationPayloadParser.RecommendationExplainabilityDocument(
            agentRun.AiTraceRecordId,
            null,
            agentRun.RetrievalRunId);

        return await CreateArtifactAsync(
            context,
            title,
            summary,
            RecommendationType.Policy,
            RecommendationCreationSource.AgentDeferred,
            RecommendationRiskState.Medium,
            RecommendationCapabilityState.ReviewRequired,
            evidence,
            suggestedActions,
            [],
            explainability,
            new RecommendationPayloadParser.RecommendationSourceReferenceDocument("agent_run", agentRun.Id),
            uniqueSourceKey,
            cancellationToken);
    }

    public async Task<CreateRecommendationResponse> FromWorkflowRunAsync(Guid workflowRunId, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var workflowRun = await dbContext.WorkflowRuns
            .SingleOrDefaultAsync(item => item.Id == workflowRunId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Workflow run was not found.");

        var uniqueSourceKey = $"workflow:{workflowRun.Id}";
        var existing = await FindByUniqueSourceKeyAsync(context.TenantId, uniqueSourceKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var childAgentRuns = await dbContext.AgentRuns
            .AsNoTracking()
            .Where(item => item.ParentWorkflowRunId == workflowRun.Id && item.TenantId == context.TenantId)
            .OrderBy(item => item.StartedAt)
            .ToListAsync(cancellationToken);

        var childToolRuns = await dbContext.ToolRuns
            .AsNoTracking()
            .Where(item => item.ParentWorkflowRunId == workflowRun.Id && item.TenantId == context.TenantId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        if (childAgentRuns.Count == 0 && childToolRuns.Count == 0)
        {
            throw new RequestValidationException("Workflow run does not contain child agent or tool runs for recommendation creation.");
        }

        foreach (var agentRun in childAgentRuns.Where(item => !string.IsNullOrWhiteSpace(item.StructuredOutputJson)))
        {
            GuardStructuredOutputAgainstDecisionCreation(agentRun.StructuredOutputJson!);
        }

        var evidence = new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>();
        foreach (var agentRun in childAgentRuns)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.AgentRun,
                agentRun.Id,
                agentRun.OutputSafeSummaryJson ?? $"Agent run {agentRun.Status}.",
                TrustState.Provisional,
                false));

            if (agentRun.AiTraceRecordId is Guid aiTraceId)
            {
                evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                    Guid.NewGuid(),
                    EvidenceLinkType.AiTrace,
                    aiTraceId,
                    "AI trace evidence from workflow child agent run.",
                    TrustState.Provisional,
                    false));
            }
        }

        foreach (var toolRun in childToolRuns)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.ToolRun,
                toolRun.Id,
                toolRun.OutputSafeSummaryJson ?? $"Tool run {toolRun.Status}.",
                TrustState.Provisional,
                false));
        }

        evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
            Guid.NewGuid(),
            EvidenceLinkType.WorkflowRun,
            workflowRun.Id,
            workflowRun.OutputSafeSummaryJson ?? $"Workflow run {workflowRun.Status}.",
            TrustState.Provisional,
            false));

        if (workflowRun.AiTraceRecordId is Guid workflowTraceId)
        {
            evidence.Add(new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                EvidenceLinkType.AiTrace,
                workflowTraceId,
                "AI trace evidence from workflow execution.",
                TrustState.Provisional,
                false));
        }

        var title = $"Workflow recommendation: {workflowRun.Id}";
        var summary = workflowRun.OutputSafeSummaryJson
            ?? childAgentRuns.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.OutputSafeSummaryJson))?.OutputSafeSummaryJson
            ?? "Recommendation created from governed workflow execution.";

        var suggestedActions = childAgentRuns
            .Where(item => !string.IsNullOrWhiteSpace(item.StructuredOutputJson))
            .SelectMany(item =>
            {
                try
                {
                    using var document = JsonDocument.Parse(item.StructuredOutputJson!);
                    return BuildSuggestedActionsFromOutput(document.RootElement, summary);
                }
                catch (JsonException)
                {
                    return Array.Empty<RecommendationPayloadParser.RecommendationSuggestedActionDocument>();
                }
            })
            .Take(5)
            .ToList();

        if (suggestedActions.Count == 0)
        {
            suggestedActions = BuildSuggestedActionsFromOutput(JsonDocument.Parse("{}").RootElement, summary).ToList();
        }

        var explainability = new RecommendationPayloadParser.RecommendationExplainabilityDocument(
            workflowRun.AiTraceRecordId,
            null,
            childAgentRuns.FirstOrDefault(item => item.RetrievalRunId is not null)?.RetrievalRunId);

        return await CreateArtifactAsync(
            context,
            title,
            summary,
            RecommendationType.Policy,
            RecommendationCreationSource.AgentDeferred,
            RecommendationRiskState.Medium,
            RecommendationCapabilityState.ReviewRequired,
            evidence,
            suggestedActions,
            [],
            explainability,
            new RecommendationPayloadParser.RecommendationSourceReferenceDocument("workflow_run", workflowRun.Id),
            uniqueSourceKey,
            cancellationToken);
    }

    private async Task<CreateRecommendationResponse> CreateArtifactAsync(
        ActiveTenantContext context,
        string title,
        string summary,
        RecommendationType recommendationType,
        RecommendationCreationSource creationSource,
        RecommendationRiskState riskState,
        RecommendationCapabilityState capabilityState,
        IReadOnlyCollection<RecommendationPayloadParser.RecommendationEvidenceLinkDocument> evidenceLinks,
        IReadOnlyCollection<RecommendationPayloadParser.RecommendationSuggestedActionDocument> suggestedActions,
        IReadOnlyCollection<RecommendationPayloadParser.RecommendationRelatedObjectDocument> relatedObjects,
        RecommendationPayloadParser.RecommendationExplainabilityDocument? explainability,
        RecommendationPayloadParser.RecommendationSourceReferenceDocument? sourceReference,
        string uniqueSourceKey,
        CancellationToken cancellationToken)
    {
        var payload = RecommendationPayloadParser.CreateDefault(
            title,
            summary,
            recommendationType,
            creationSource,
            riskState,
            capabilityState,
            evidenceLinks,
            suggestedActions,
            relatedObjects,
            explainability,
            true,
            sourceReference,
            uniqueSourceKey);

        var versionLabel = $"rec-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = RecommendationArtifactTypes.Recommendation,
            NormalizedArtifactType = RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            Name = title,
            Description = summary,
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = versionLabel,
            NormalizedVersionLabel = versionLabel.ToUpperInvariant(),
            Summary = summary,
            PayloadJson = RecommendationPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
            CompatibilityStatus = ArtifactCompatibilityStatus.Unknown,
            PolicyRiskStatus = ArtifactPolicyRiskStatus.NotEvaluated,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateRecommendationResponse(
            artifact.Id,
            version.Id,
            version.VersionLabel,
            payload.LifecycleStatus);
    }

    private async Task<CreateRecommendationResponse?> FindByUniqueSourceKeyAsync(
        Guid tenantId,
        string uniqueSourceKey,
        CancellationToken cancellationToken)
    {
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.TenantId == tenantId)
            .Join(
                dbContext.Artifacts.Where(artifact => artifact.NormalizedArtifactType == RecommendationArtifactTypes.Recommendation.ToUpperInvariant()),
                version => version.ArtifactId,
                artifact => artifact.Id,
                (version, artifact) => new { version, artifact })
            .OrderByDescending(pair => pair.version.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var pair in versions)
        {
            var payload = RecommendationPayloadParser.Deserialize(pair.version.PayloadJson ?? "{}");
            if (string.Equals(payload.UniqueSourceKey, uniqueSourceKey, StringComparison.OrdinalIgnoreCase))
            {
                return new CreateRecommendationResponse(
                    pair.artifact.Id,
                    pair.version.Id,
                    pair.version.VersionLabel,
                    payload.LifecycleStatus);
            }
        }

        return null;
    }

    private async Task AddRelationshipAsync(
        Guid tenantId,
        Guid sourceArtifactId,
        Guid targetArtifactId,
        string description,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ArtifactRelationships.AnyAsync(
            relationship => relationship.TenantId == tenantId
                && relationship.SourceArtifactId == sourceArtifactId
                && relationship.TargetArtifactId == targetArtifactId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.ArtifactRelationships.Add(new ArtifactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceArtifactId = sourceArtifactId,
            TargetArtifactId = targetArtifactId,
            RelationshipType = ArtifactRelationshipType.DerivedFrom,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("recommendations.create", cancellationToken);
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Create, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "recommendations.create",
            "permission_denied",
            $"The user lacks the {RecommendationPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks recommendation create permission.");
    }

    private async Task<ModelPackageRecommendationTemplates?> LoadRecommendationTemplatesAsync(
        ActiveTenantContext context,
        Guid importBatchId,
        CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == importBatchId && item.TenantId == context.TenantId, cancellationToken);
        if (batch is null)
        {
            return null;
        }

        var packageContext = await modelPackageContextResolver.ResolvePublishedAsync(
            batch.ActiveModelPackageVersionId,
            context,
            "recommendations.bom_comparison",
            cancellationToken);
        return packageContext.ImportProfile.RecommendationTemplates;
    }

    private static string FormatTemplate(string? template, string fallback, IReadOnlyDictionary<string, string> values)
    {
        var resolved = string.IsNullOrWhiteSpace(template) ? fallback : template;
        foreach (var (key, value) in values)
        {
            resolved = resolved.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return resolved;
    }

    private static RecommendationRiskState MapSeverityToRisk(DataQualitySeverity severity)
        => severity switch
        {
            DataQualitySeverity.Critical => RecommendationRiskState.Critical,
            DataQualitySeverity.High => RecommendationRiskState.High,
            DataQualitySeverity.Medium => RecommendationRiskState.Medium,
            _ => RecommendationRiskState.Low
        };

    private static IReadOnlyCollection<RecommendationPayloadParser.RecommendationSuggestedActionDocument> BuildSuggestedActionsFromOutput(
        JsonElement output,
        string fallbackRationale)
    {
        if (!output.TryGetProperty("suggestedActions", out var actionsElement) || actionsElement.ValueKind != JsonValueKind.Array)
        {
            return
            [
                new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                    Guid.NewGuid(),
                    "Review agent recommendation",
                    "REVIEW_AGENT_RECOMMENDATION",
                    RecommendationRiskState.Medium,
                    "DOMAIN_REVIEW",
                    SuggestedActionStatus.Proposed,
                    fallbackRationale)
            ];
        }

        var actions = new List<RecommendationPayloadParser.RecommendationSuggestedActionDocument>();
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            var title = ReadStringProperty(actionElement, "title", "name") ?? "Review agent recommendation";
            var code = ReadStringProperty(actionElement, "code", "actionCode") ?? "REVIEW_AGENT_RECOMMENDATION";
            var rationale = ReadStringProperty(actionElement, "rationale", "summary") ?? fallbackRationale;
            actions.Add(new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                Guid.NewGuid(),
                title,
                code,
                RecommendationRiskState.Medium,
                "DOMAIN_REVIEW",
                SuggestedActionStatus.Proposed,
                rationale));
        }

        return actions.Count == 0
            ?
            [
                new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                    Guid.NewGuid(),
                    "Review agent recommendation",
                    "REVIEW_AGENT_RECOMMENDATION",
                    RecommendationRiskState.Medium,
                    "DOMAIN_REVIEW",
                    SuggestedActionStatus.Proposed,
                    fallbackRationale)
            ]
            : actions;
    }

    private async Task<string?> LoadOutputSchemaPayloadAsync(
        Guid tenantId,
        string? agentVersionPayloadJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentVersionPayloadJson))
        {
            return null;
        }

        var agentPayload = AgentDefinitionPayloadParser.Deserialize(agentVersionPayloadJson);
        if (agentPayload.OutputSchemaVersionId is not Guid outputSchemaVersionId || outputSchemaVersionId == Guid.Empty)
        {
            return null;
        }

        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == outputSchemaVersionId && item.TenantId == tenantId, cancellationToken);
        return version?.PayloadJson;
    }

    private static bool OutputSchemaCreatesDecision(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static void GuardStructuredOutputAgainstDecisionCreation(string structuredOutputJson)
    {
        using var document = JsonDocument.Parse(structuredOutputJson);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.True)
            {
                throw new RequestValidationException("Agent structured output must not create decision artifacts.");
            }
        }
    }

    private static string? ReadStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }
}
