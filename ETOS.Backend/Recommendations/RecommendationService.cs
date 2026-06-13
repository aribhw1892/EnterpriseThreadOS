using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Recommendations;

public interface IRecommendationService
{
    Task<IReadOnlyCollection<RecommendationArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<RecommendationPayloadResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateRecommendationResponse> CreateAsync(CreateRecommendationRequest request, CancellationToken cancellationToken);
    Task<MarkRecommendationReviewedResponse> MarkReviewedAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<MarkRecommendationReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<UpdateSuggestedActionStatusResponse> UpdateSuggestedActionStatusAsync(
        Guid artifactId,
        Guid versionId,
        Guid actionId,
        UpdateSuggestedActionStatusRequest request,
        CancellationToken cancellationToken);
}

public sealed class RecommendationService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IRecommendationEvidenceResolver evidenceResolver) : IRecommendationService
{
    public async Task<IReadOnlyCollection<RecommendationArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("recommendations.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("recommendations.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == RecommendationArtifactTypes.Recommendation.ToUpperInvariant())
            .OrderByDescending(item => item.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(item => item.Id).ToArray();
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
            RecommendationLifecycleStatus? lifecycleStatus = null;
            RecommendationType? recommendationType = null;
            if (version?.PayloadJson is not null)
            {
                var payload = RecommendationPayloadParser.Deserialize(version.PayloadJson);
                lifecycleStatus = payload.LifecycleStatus;
                recommendationType = payload.RecommendationType;
            }

            return new RecommendationArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                lifecycleStatus,
                recommendationType,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<RecommendationPayloadResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "recommendations.get", cancellationToken);
        return RecommendationPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}");
    }

    public async Task<CreateRecommendationResponse> CreateAsync(CreateRecommendationRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);

        if (request.EvidenceLinks.Count == 0)
        {
            throw new RequestValidationException("At least one evidence link is required.");
        }

        var evidenceLinks = request.EvidenceLinks.Select(link =>
            new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                Guid.NewGuid(),
                link.EvidenceType,
                link.SourceId,
                link.SafeSummary.Trim(),
                link.TrustState ?? TrustState.Provisional,
                link.PermissionFiltered)).ToList();

        var enriched = await evidenceResolver.EnrichEvidenceLinksAsync(
            context.TenantId,
            evidenceLinks.Select(link => new RecommendationEvidenceLinkResponse(
                link.LinkId,
                link.EvidenceType,
                link.SourceId,
                link.SafeSummary,
                link.TrustState,
                link.PermissionFiltered)).ToList(),
            cancellationToken);

        var enrichedDocuments = enriched.Select(link =>
            new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                link.LinkId,
                link.EvidenceType,
                link.SourceId,
                link.SafeSummary,
                link.TrustState,
                link.PermissionFiltered)).ToList();

        var suggestedActions = request.SuggestedActions.Select(action =>
            new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                Guid.NewGuid(),
                action.Title.Trim(),
                action.Kind.Trim(),
                action.RiskScore,
                action.RequiredReviewPath?.Trim(),
                SuggestedActionStatus.Proposed,
                action.Description?.Trim())).ToList();

        var relatedObjects = request.RelatedObjects?.Select(item =>
            new RecommendationPayloadParser.RecommendationRelatedObjectDocument(item.GraphNodeId, item.ObjectType)).ToList()
            ?? [];

        var payload = RecommendationPayloadParser.CreateDefault(
            request.Title,
            request.Summary,
            request.RecommendationType,
            request.CreationSource,
            request.RiskState,
            request.CapabilityState,
            enrichedDocuments,
            suggestedActions,
            relatedObjects,
            request.Explainability is null
                ? null
                : new RecommendationPayloadParser.RecommendationExplainabilityDocument(
                    request.Explainability.AiTraceId,
                    request.Explainability.ContextPackageId,
                    request.Explainability.RetrievalRunId),
            request.OutcomeTrackingRequired,
            null,
            null);

        var draftPayload = RecommendationPayloadParser.Parse(
            Guid.Empty,
            Guid.Empty,
            "draft",
            ArtifactReadinessState.Draft.ToString(),
            RecommendationPayloadParser.Serialize(payload));
        var reviewNotes = RecommendationReadinessValidator.ValidateForReviewed(draftPayload);
        if (reviewNotes.Count > 0)
        {
            throw new RequestValidationException(string.Join(" ", reviewNotes));
        }

        var versionLabel = $"rec-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = RecommendationArtifactTypes.Recommendation,
            NormalizedArtifactType = RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            Name = request.Title.Trim(),
            Description = request.Summary.Trim(),
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
            Summary = request.Summary.Trim(),
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

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "recommendations.create",
                AuditResult.Success,
                null,
                $"Recommendation '{artifact.Name}' version '{version.VersionLabel}' created.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateRecommendationResponse(
            artifact.Id,
            version.Id,
            version.VersionLabel,
            payload.LifecycleStatus);
    }

    public async Task<MarkRecommendationReviewedResponse> MarkReviewedAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReviewPermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "recommendations.review.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "recommendations.review.mark", cancellationToken);

        var payloadDocument = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var payload = RecommendationPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}");

        var validationNotes = RecommendationReadinessValidator.ValidateForReviewed(payload);
        if (validationNotes.Count > 0)
        {
            throw new RequestValidationException(string.Join(" ", validationNotes));
        }

        payloadDocument.LifecycleStatus = RecommendationLifecycleStatus.Reviewed;
        version.PayloadJson = RecommendationPayloadParser.Serialize(payloadDocument);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "recommendations.review.mark",
                AuditResult.Success,
                null,
                $"Recommendation version '{version.VersionLabel}' marked reviewed.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkRecommendationReviewedResponse(
            artifactId,
            versionId,
            RecommendationLifecycleStatus.Reviewed,
            validationNotes);
    }

    public async Task<MarkRecommendationReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "recommendations.readiness.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "recommendations.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var payloadDocument = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var enriched = await evidenceResolver.EnrichEvidenceLinksAsync(
            context.TenantId,
            payloadDocument.EvidenceLinks.Select(link => new RecommendationEvidenceLinkResponse(
                link.LinkId,
                link.EvidenceType,
                link.SourceId,
                link.SafeSummary,
                link.TrustState,
                link.PermissionFiltered)).ToList(),
            cancellationToken);

        payloadDocument.EvidenceLinks = enriched.Select(link =>
            new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                link.LinkId,
                link.EvidenceType,
                link.SourceId,
                link.SafeSummary,
                link.TrustState,
                link.PermissionFiltered)).ToList();

        var payload = RecommendationPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            version.ReadinessState.ToString(),
            RecommendationPayloadParser.Serialize(payloadDocument));

        var excluded = await evidenceResolver.HasExcludedFromTrustedRecommendationsAsync(
            context.TenantId,
            payload.EvidenceLinks,
            cancellationToken);

        var validationNotes = RecommendationReadinessValidator.ValidateForReady(payload, excluded);
        if (validationNotes.Count > 0)
        {
            throw new RequestValidationException(string.Join(" ", validationNotes));
        }

        payloadDocument.TrustState = RecommendationPayloadParser.ComputeAggregateTrustState(payloadDocument.EvidenceLinks);
        payloadDocument.ConflictState = RecommendationPayloadParser.ComputeConflictState(payloadDocument.EvidenceLinks);
        version.PayloadJson = RecommendationPayloadParser.Serialize(payloadDocument);

        await classificationPolicyService.EvaluateArtifactPolicyRiskAsync(context.TenantId, version.Id, cancellationToken);
        await dbContext.Entry(version).ReloadAsync(cancellationToken);

        version.ReadinessState = version.PolicyRiskStatus switch
        {
            ArtifactPolicyRiskStatus.RequiresApproval => ArtifactReadinessState.RequiresApproval,
            ArtifactPolicyRiskStatus.Blocked => ArtifactReadinessState.Blocked,
            _ => ArtifactReadinessState.Ready
        };
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "recommendations.readiness.mark",
                AuditResult.Success,
                null,
                $"Recommendation version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkRecommendationReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            payloadDocument.TrustState,
            payloadDocument.ConflictState,
            validationNotes);
    }

    public async Task<UpdateSuggestedActionStatusResponse> UpdateSuggestedActionStatusAsync(
        Guid artifactId,
        Guid versionId,
        Guid actionId,
        UpdateSuggestedActionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireReviewPermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "recommendations.suggested_action.update", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "recommendations.suggested_action.update", cancellationToken);

        var payloadDocument = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var action = payloadDocument.SuggestedActions.SingleOrDefault(item => item.ActionId == actionId)
            ?? throw new RequestValidationException("Suggested action was not found.");

        var updatedAction = action with { Status = request.Status };
        payloadDocument.SuggestedActions = payloadDocument.SuggestedActions
            .Select(item => item.ActionId == actionId ? updatedAction : item)
            .ToList();

        version.PayloadJson = RecommendationPayloadParser.Serialize(payloadDocument);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "recommendations.suggested_action.update",
                AuditResult.Success,
                null,
                $"Suggested action '{actionId}' updated to {request.Status}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new UpdateSuggestedActionStatusResponse(artifactId, versionId, actionId, request.Status);
    }

    private async Task<(Artifact Artifact, ArtifactVersion Version)> RequireVersionAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync(action, cancellationToken);
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        if (!artifact.ArtifactType.Equals(RecommendationArtifactTypes.Recommendation, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a recommendation.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        if (version.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        return (artifact, version);
    }

    private async Task RequireOwnerOrAdminAsync(
        ActiveTenantContext context,
        Artifact artifact,
        string action,
        CancellationToken cancellationToken)
    {
        if (artifact.OwnerUserId == context.UserId
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            "Only an artifact owner or administrator may update recommendation versions.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks artifact ownership or administration permission.");
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await HasReadPermissionAsync(context, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {RecommendationPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks recommendation read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("recommendations.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
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

    private async Task<ActiveTenantContext> RequireReviewPermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("recommendations.review", cancellationToken);
        if (await HasReviewPermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "recommendations.review",
            "permission_denied",
            $"The user lacks the {RecommendationPermissions.Review} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks recommendation review permission.");
    }

    private async Task<ActiveTenantContext> RequireReadinessPermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("recommendations.readiness", cancellationToken);
        if (await HasReadinessPermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "recommendations.readiness",
            "permission_denied",
            $"The user lacks the {RecommendationPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks recommendation readiness permission.");
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", "Record belongs to a different tenant.", cancellationToken);
        throw new TenantAccessDeniedException("Record is not available in the active tenant.");
    }

    private async Task<bool> HasReadPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Read, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReviewPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Review, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, RecommendationPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
}
