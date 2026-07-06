using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ReviewTasks;

public interface IReviewTaskTemplateService
{
    Task<IReadOnlyCollection<ReviewTaskTemplateArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ReviewTaskTemplateDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateReviewTaskTemplateResponse> CreateAsync(CreateReviewTaskTemplateRequest request, CancellationToken cancellationToken);
    Task<CreateReviewTaskTemplateVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateReviewTaskTemplateVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkReviewTaskTemplateReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishReviewTaskTemplateResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ReviewTaskTemplateService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService) : IReviewTaskTemplateService
{
    public async Task<IReadOnlyCollection<ReviewTaskTemplateArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("review-task-templates.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("review-task-templates.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant())
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
            string? templateKey = null;
            string? reviewTaskType = null;
            if (version?.PayloadJson is not null)
            {
                var payload = ReviewTaskTemplatePayloadParser.Deserialize(version.PayloadJson);
                templateKey = payload.TemplateKey;
                reviewTaskType = payload.ReviewTaskType;
            }

            return new ReviewTaskTemplateArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                templateKey,
                reviewTaskType,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<ReviewTaskTemplateDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-task-templates.get", cancellationToken);
        return ReviewTaskTemplatePayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}");
    }

    public async Task<CreateReviewTaskTemplateResponse> CreateAsync(
        CreateReviewTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request);
        ReviewTaskTemplatePayloadParser.ValidateCore(payload);

        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate,
            NormalizedArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = TrimOptional(request.Description),
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
            Summary = request.ReviewTaskType.Trim(),
            PayloadJson = ReviewTaskTemplatePayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
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
                "review-task-templates.create",
                AuditResult.Success,
                null,
                $"Review task template '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateReviewTaskTemplateResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateReviewTaskTemplateVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateReviewTaskTemplateVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "review-task-templates.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request);
        ReviewTaskTemplatePayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary ?? request.ReviewTaskType),
            PayloadJson = ReviewTaskTemplatePayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateReviewTaskTemplateVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkReviewTaskTemplateReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("review-task-templates.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-task-templates.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = ReviewTaskTemplatePayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var validationNotes = ReviewTaskTemplateReadinessValidator.ValidateRequiredFields(document).ToList();
        if (validationNotes.Count > 0)
        {
            throw new RequestValidationException(string.Join(" ", validationNotes));
        }

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

        return new MarkReviewTaskTemplateReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<PublishReviewTaskTemplateResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "review-task-templates.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishReviewTaskTemplateResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    private static ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument BuildPayload(
        CreateReviewTaskTemplateRequest request)
        => ReviewTaskTemplatePayloadParser.Create(
            request.TemplateKey,
            request.ReviewTaskType,
            request.PriorityRules?.Select(item => new ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePriorityRuleDocument
            {
                SeverityWeight = item.SeverityWeight,
                TrustWeight = item.TrustWeight,
                ConflictWeight = item.ConflictWeight
            }).ToList(),
            request.RequiresDataQualityPrerequisite,
            request.EscalationPath is null
                ? null
                : new ReviewTaskTemplatePayloadParser.ReviewTaskTemplateEscalationPathDocument
                {
                    Enabled = request.EscalationPath.Enabled,
                    EscalationTargetRoleKey = request.EscalationPath.EscalationTargetRoleKey,
                    EscalationPolicyId = request.EscalationPath.EscalationPolicyId,
                    SlaPolicyVersion = request.EscalationPath.SlaPolicyVersion,
                    CanOverrideOriginalOutcome = request.EscalationPath.CanOverrideOriginalOutcome
                },
            ToApprovalRuleDocument(request.ApprovalRule),
            request.ParticipantRoleDefaults,
            request.AllowedOutcomeOptions);

    private static ReviewTaskTemplatePayloadParser.ReviewTaskTemplateApprovalRuleDocument? ToApprovalRuleDocument(
        ReviewTaskTemplateApprovalRuleRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        return new ReviewTaskTemplatePayloadParser.ReviewTaskTemplateApprovalRuleDocument
        {
            Mode = request.Mode,
            RequiredRoles = request.RequiredRoles?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            OutcomeTaxonomyVersionId = request.OutcomeTaxonomyVersionId,
            OutcomeTrackingRequired = request.OutcomeTrackingRequired
        };
    }

    private static ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument BuildPayload(
        CreateReviewTaskTemplateVersionRequest request)
        => ReviewTaskTemplatePayloadParser.Create(
            request.TemplateKey,
            request.ReviewTaskType,
            request.PriorityRules?.Select(item => new ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePriorityRuleDocument
            {
                SeverityWeight = item.SeverityWeight,
                TrustWeight = item.TrustWeight,
                ConflictWeight = item.ConflictWeight
            }).ToList(),
            request.RequiresDataQualityPrerequisite,
            request.EscalationPath is null
                ? null
                : new ReviewTaskTemplatePayloadParser.ReviewTaskTemplateEscalationPathDocument
                {
                    Enabled = request.EscalationPath.Enabled,
                    EscalationTargetRoleKey = request.EscalationPath.EscalationTargetRoleKey,
                    EscalationPolicyId = request.EscalationPath.EscalationPolicyId,
                    SlaPolicyVersion = request.EscalationPath.SlaPolicyVersion,
                    CanOverrideOriginalOutcome = request.EscalationPath.CanOverrideOriginalOutcome
                },
            ToApprovalRuleDocument(request.ApprovalRule),
            request.ParticipantRoleDefaults,
            request.AllowedOutcomeOptions);

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

        if (!artifact.ArtifactType.Equals(ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a review task template.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        return (artifact, version);
    }

    private async Task<Artifact> RequireArtifactAsync(
        Guid artifactId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        return artifact;
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskTemplatePermissions.Read, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "missing_permission", "Review task template read permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Review task template read permission is required.");
        }
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("review-task-templates.create", cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskTemplatePermissions.Create, cancellationToken))
        {
            throw new TenantAccessDeniedException("Review task template create permission is required.");
        }

        return context;
    }

    private async Task<ActiveTenantContext> RequireReadinessPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskTemplatePermissions.Readiness, cancellationToken))
        {
            throw new TenantAccessDeniedException("Review task template readiness permission is required.");
        }

        return context;
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_mismatch", "The requested record belongs to a different tenant.", cancellationToken);
        throw new TenantAccessDeniedException("The requested record belongs to a different tenant.");
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
