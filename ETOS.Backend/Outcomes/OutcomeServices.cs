using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.Outcomes;
using ETOS.Backend.ReviewTasks;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Outcomes;

public interface IOutcomeTaxonomyService
{
    Task<IReadOnlyCollection<OutcomeTaxonomyDetailResponse>> ListAsync(CancellationToken cancellationToken);
    Task<OutcomeTaxonomyDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateOutcomeTaxonomyResponse> CreateAsync(CreateOutcomeTaxonomyRequest request, CancellationToken cancellationToken);
}

public interface IOutcomeService
{
    Task<RecordManualOutcomeResponse> RecordManualOutcomeAsync(
        Guid decisionArtifactId,
        Guid decisionVersionId,
        RecordManualOutcomeRequest request,
        CancellationToken cancellationToken);
}

public sealed class OutcomeTaxonomyService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IOutcomeTaxonomyService
{
    public async Task<IReadOnlyCollection<OutcomeTaxonomyDetailResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("outcome-taxonomies.list", cancellationToken);
        var normalizedType = OutcomeTaxonomyArtifactTypes.OutcomeTaxonomy.ToUpperInvariant();
        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId && artifact.NormalizedArtifactType == normalizedType)
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var responses = new List<OutcomeTaxonomyDetailResponse>();
        foreach (var artifact in artifacts)
        {
            var version = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Where(item => item.ArtifactId == artifact.Id)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (version is null)
            {
                continue;
            }

            responses.Add(ToDetail(artifact, version));
        }

        return responses;
    }

    public async Task<OutcomeTaxonomyDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("outcome-taxonomies.get", cancellationToken);
        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == artifactId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");
        return ToDetail(artifact, version);
    }

    public async Task<CreateOutcomeTaxonomyResponse> CreateAsync(
        CreateOutcomeTaxonomyRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireAdminAsync("outcome-taxonomies.create", cancellationToken);
        var payload = OutcomeTaxonomyPayloadParser.Create(request.TaxonomyKey, request.Categories);
        OutcomeTaxonomyPayloadParser.ValidateCore(payload);

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = OutcomeTaxonomyArtifactTypes.OutcomeTaxonomy,
            NormalizedArtifactType = OutcomeTaxonomyArtifactTypes.OutcomeTaxonomy.ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
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
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = request.TaxonomyKey.Trim(),
            PayloadJson = OutcomeTaxonomyPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateOutcomeTaxonomyResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    private static OutcomeTaxonomyDetailResponse ToDetail(Artifact artifact, ArtifactVersion version)
    {
        var payload = OutcomeTaxonomyPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        return new OutcomeTaxonomyDetailResponse(
            artifact.Id,
            version.Id,
            version.VersionLabel,
            artifact.Name,
            payload.TaxonomyKey ?? string.Empty,
            payload.Categories ?? [],
            version.ReadinessState.ToString());
    }

    private async Task<ActiveTenantContext> RequireReadAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OutcomePermissions.Read, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OutcomePermissions.Admin, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            throw new TenantAccessDeniedException("Outcome read permission is required.");
        }

        return context;
    }

    private async Task<ActiveTenantContext> RequireAdminAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OutcomePermissions.Admin, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            throw new TenantAccessDeniedException("Outcome admin permission is required.");
        }

        return context;
    }
}

public sealed class OutcomeService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    ILearningEvidenceEmitter learningEvidenceEmitter) : IOutcomeService
{
    public async Task<RecordManualOutcomeResponse> RecordManualOutcomeAsync(
        Guid decisionArtifactId,
        Guid decisionVersionId,
        RecordManualOutcomeRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireRecordAsync("decisions.outcomes.record", cancellationToken);
        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == decisionVersionId && item.ArtifactId == decisionArtifactId, cancellationToken)
            ?? throw new RequestValidationException("Decision version was not found.");

        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == decisionArtifactId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Decision artifact was not found.");

        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        if (payload.OutcomeTaxonomyVersionId.HasValue)
        {
            await ValidateTaxonomyCategoryAsync(context.TenantId, payload.OutcomeTaxonomyVersionId.Value, request.ActualOutcome, cancellationToken);
        }

        var outcomeRun = new OutcomeCheckRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DecisionArtifactId = decisionArtifactId,
            CheckType = request.CheckType.Trim(),
            ExpectedOutcome = request.ExpectedOutcome.Trim(),
            ActualOutcome = request.ActualOutcome.Trim(),
            OutcomeStatus = request.OutcomeStatus,
            OutcomeConfidence = request.OutcomeConfidence,
            MeasuredAt = DateTimeOffset.UtcNow,
            EvidenceSummary = string.IsNullOrWhiteSpace(request.EvidenceSummary)
                ? $"Manual outcome recorded for decision {decisionArtifactId}."
                : request.EvidenceSummary.Trim(),
            RecordedByUserId = context.UserId,
            RecommendationArtifactId = request.RecommendationArtifactId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.OutcomeCheckRuns.Add(outcomeRun);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.RecommendationArtifactId.HasValue)
        {
            dbContext.ArtifactRelationships.Add(new ArtifactRelationship
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                SourceArtifactId = decisionArtifactId,
                TargetArtifactId = request.RecommendationArtifactId.Value,
                RelationshipType = ArtifactRelationshipType.References,
                Description = "Manual outcome linked to recommendation.",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await learningEvidenceEmitter.EmitDecisionEvidenceAsync(
            context.TenantId,
            decisionArtifactId,
            payload,
            true,
            cancellationToken);

        return new RecordManualOutcomeResponse(
            outcomeRun.Id,
            decisionArtifactId,
            decisionVersionId,
            ToResponse(outcomeRun));
    }

    private async Task ValidateTaxonomyCategoryAsync(
        Guid tenantId,
        Guid taxonomyVersionId,
        string actualOutcome,
        CancellationToken cancellationToken)
    {
        var taxonomyVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(version => version.Id == taxonomyVersionId && version.TenantId == tenantId, cancellationToken);
        if (taxonomyVersion?.PayloadJson is null)
        {
            return;
        }

        var taxonomy = OutcomeTaxonomyPayloadParser.Deserialize(taxonomyVersion.PayloadJson);
        if (taxonomy.Categories?.Any(category => category.Equals(actualOutcome, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return;
        }

        throw new RequestValidationException($"Actual outcome '{actualOutcome}' is not in the linked outcome taxonomy.");
    }

    private static OutcomeCheckRunResponse ToResponse(OutcomeCheckRun run)
        => new(
            run.Id,
            run.DecisionArtifactId,
            run.CheckType,
            run.ExpectedOutcome,
            run.ActualOutcome,
            run.OutcomeStatus,
            run.OutcomeConfidence,
            run.MeasuredAt,
            run.EvidenceSummary,
            run.RecordedByUserId);

    private async Task<ActiveTenantContext> RequireRecordAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OutcomePermissions.Record, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OutcomePermissions.Admin, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "missing_permission", "Outcome record permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Outcome record permission is required.");
        }

        return context;
    }
}
