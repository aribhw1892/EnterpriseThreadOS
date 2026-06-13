using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
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
}

public sealed class RecommendationFactory(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IRecommendationFactory
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

        var driftCount = run.MissingInEbomCount + run.QuantityMismatchCount + run.UsageReferenceMismatchCount;
        if (driftCount == 0 && run.MissingInCadCount == 0)
        {
            throw new RequestValidationException("BOM comparison run has no drift requiring a recommendation.");
        }

        var uniqueSourceKey = $"bom:{run.Id}:BOM_SYNC";
        var existing = await FindByUniqueSourceKeyAsync(context.TenantId, uniqueSourceKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var summary =
            $"CAD/EBOM drift detected. Missing in EBOM {run.MissingInEbomCount}, missing in CAD {run.MissingInCadCount}, quantity mismatches {run.QuantityMismatchCount}.";

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
                "Review EBOM synchronization",
                "REVIEW_EBOM",
                run.UnresolvedIdentityCount > 0 ? RecommendationRiskState.High : RecommendationRiskState.Medium,
                "ENGINEERING_REVIEW",
                SuggestedActionStatus.Proposed,
                "Validate whether EBOM should be updated to match CAD BOM."),
            new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                Guid.NewGuid(),
                "Review manufacturing impact",
                "REVIEW_MANUFACTURING_IMPACT",
                RecommendationRiskState.High,
                "MANUFACTURING_REVIEW",
                SuggestedActionStatus.Proposed,
                "Assess downstream manufacturing impact from BOM drift.")
        };

        var response = await CreateArtifactAsync(
            context,
            "Review CAD/EBOM synchronization",
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

    private static RecommendationRiskState MapSeverityToRisk(DataQualitySeverity severity)
        => severity switch
        {
            DataQualitySeverity.Critical => RecommendationRiskState.Critical,
            DataQualitySeverity.High => RecommendationRiskState.High,
            DataQualitySeverity.Medium => RecommendationRiskState.Medium,
            _ => RecommendationRiskState.Low
        };
}
