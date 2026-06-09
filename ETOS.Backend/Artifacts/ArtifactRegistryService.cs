using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Artifacts;

public interface IArtifactRegistryService
{
    Task<IReadOnlyCollection<ArtifactSummaryResponse>> ListArtifactsAsync(CancellationToken cancellationToken);

    Task<ArtifactDetailResponse> GetArtifactAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<ArtifactSummaryResponse> CreateArtifactAsync(CreateArtifactRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArtifactVersionSummaryResponse>> ListVersionsAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<ArtifactVersionSummaryResponse> CreateVersionAsync(Guid artifactId, CreateArtifactVersionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArtifactRelationshipResponse>> ListRelationshipsAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<ArtifactRelationshipResponse> AddRelationshipAsync(Guid artifactId, CreateArtifactRelationshipRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArtifactDependencyResponse>> ListDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);

    Task<ArtifactDependencyResponse> AddDependencyAsync(
        Guid artifactId,
        Guid versionId,
        CreateArtifactDependencyRequest request,
        CancellationToken cancellationToken);

    Task<ArtifactImpactResponse> GetImpactAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);

    Task<ArtifactReadinessResponse> GetReadinessAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);

    Task<PublishArtifactVersionResult> PublishVersionAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ArtifactRegistryService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder) : IArtifactRegistryService
{
    private static readonly CreateArtifactRequestValidator CreateArtifactValidator = new();
    private static readonly CreateArtifactVersionRequestValidator CreateVersionValidator = new();
    private static readonly CreateArtifactRelationshipRequestValidator CreateRelationshipValidator = new();
    private static readonly CreateArtifactDependencyRequestValidator CreateDependencyValidator = new();

    public async Task<IReadOnlyCollection<ArtifactSummaryResponse>> ListArtifactsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.list", ArtifactPermissions.Read, cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId)
            .OrderBy(artifact => artifact.ArtifactType)
            .ThenBy(artifact => artifact.Name)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersionRows = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var latestVersions = latestVersionRows.ToDictionary(version => version.ArtifactId, ToVersionSummary);

        return artifacts
            .Select(artifact => ToArtifactSummary(artifact, latestVersions.GetValueOrDefault(artifact.Id)))
            .ToList();
    }

    public async Task<ArtifactDetailResponse> GetArtifactAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.get", ArtifactPermissions.Read, cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "artifacts.get", cancellationToken);
        return await BuildArtifactDetailAsync(artifact, cancellationToken);
    }

    public async Task<ArtifactSummaryResponse> CreateArtifactAsync(CreateArtifactRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateArtifactValidator, request, cancellationToken);
        var context = await RequireArtifactPermissionAsync("artifacts.create", ArtifactPermissions.Create, cancellationToken);
        var ownerUserId = request.OwnerUserId ?? context.UserId;

        var ownerExists = await dbContext.Users.AnyAsync(user => user.Id == ownerUserId, cancellationToken);
        if (!ownerExists)
        {
            throw new RequestValidationException("Artifact owner was not found.");
        }

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = NormalizeText(request.ArtifactType),
            NormalizedArtifactType = NormalizeKey(request.ArtifactType),
            Name = NormalizeText(request.Name),
            Description = TrimOptional(request.Description),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "artifacts.create",
                AuditResult.Success,
                null,
                $"Artifact '{artifact.Name}' was created.",
                SourceObjectType: nameof(Artifact),
                SourceObjectId: artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return ToArtifactSummary(artifact, null);
    }

    public async Task<IReadOnlyCollection<ArtifactVersionSummaryResponse>> ListVersionsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.versions.list", ArtifactPermissions.Read, cancellationToken);
        _ = await RequireArtifactAsync(artifactId, context, "artifacts.versions.list", cancellationToken);

        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.TenantId == context.TenantId && version.ArtifactId == artifactId)
            .OrderByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        return versions.Select(ToVersionSummary).ToList();
    }

    public async Task<ArtifactVersionSummaryResponse> CreateVersionAsync(
        Guid artifactId,
        CreateArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateVersionValidator, request, cancellationToken);
        var context = await RequireArtifactPermissionAsync("artifacts.versions.create", ArtifactPermissions.Create, cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "artifacts.versions.create", cancellationToken);

        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            PayloadJson = TrimOptional(request.PayloadJson),
            ReadinessState = request.ReadinessState,
            CompatibilityStatus = request.CompatibilityStatus,
            CompatibilitySummary = TrimOptional(request.CompatibilitySummary),
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "artifacts.versions.create",
                AuditResult.Success,
                null,
                $"Artifact version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                SourceObjectType: nameof(ArtifactVersion),
                SourceObjectId: version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return ToVersionSummary(version);
    }

    public async Task<IReadOnlyCollection<ArtifactRelationshipResponse>> ListRelationshipsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.relationships.list", ArtifactPermissions.Read, cancellationToken);
        _ = await RequireArtifactAsync(artifactId, context, "artifacts.relationships.list", cancellationToken);

        return await RelationshipsQuery(context.TenantId, artifactId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ArtifactRelationshipResponse> AddRelationshipAsync(
        Guid artifactId,
        CreateArtifactRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateRelationshipValidator, request, cancellationToken);
        var context = await RequireArtifactPermissionAsync("artifacts.relationships.create", ArtifactPermissions.Create, cancellationToken);
        var sourceArtifact = await RequireArtifactAsync(artifactId, context, "artifacts.relationships.create", cancellationToken);
        _ = await RequireArtifactAsync(request.TargetArtifactId, context, "artifacts.relationships.create", cancellationToken);

        if (artifactId == request.TargetArtifactId)
        {
            throw new RequestValidationException("Artifact relationships cannot target the same artifact.");
        }

        var exists = await dbContext.ArtifactRelationships.AnyAsync(
            relationship => relationship.TenantId == context.TenantId
                && relationship.SourceArtifactId == artifactId
                && relationship.TargetArtifactId == request.TargetArtifactId
                && relationship.RelationshipType == request.RelationshipType,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact relationship already exists.");
        }

        var relationship = new ArtifactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceArtifactId = artifactId,
            TargetArtifactId = request.TargetArtifactId,
            RelationshipType = request.RelationshipType,
            Description = TrimOptional(request.Description),
            CreatedAt = DateTimeOffset.UtcNow
        };

        sourceArtifact.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ArtifactRelationships.Add(relationship);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await RelationshipsQuery(context.TenantId, artifactId)
            .SingleAsync(candidate => candidate.Id == relationship.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ArtifactDependencyResponse>> ListDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.dependencies.list", ArtifactPermissions.Read, cancellationToken);
        _ = await RequireVersionAsync(artifactId, versionId, context, "artifacts.dependencies.list", cancellationToken);

        return await DependenciesQuery(context.TenantId, versionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ArtifactDependencyResponse> AddDependencyAsync(
        Guid artifactId,
        Guid versionId,
        CreateArtifactDependencyRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateDependencyValidator, request, cancellationToken);
        var context = await RequireArtifactPermissionAsync("artifacts.dependencies.create", ArtifactPermissions.Create, cancellationToken);
        _ = await RequireVersionAsync(artifactId, versionId, context, "artifacts.dependencies.create", cancellationToken);
        _ = await RequireVersionAsync(
            request.RequiredArtifactId,
            request.RequiredVersionId,
            context,
            "artifacts.dependencies.create",
            cancellationToken);

        if (artifactId == request.RequiredArtifactId)
        {
            throw new RequestValidationException("Artifact versions cannot depend on versions of the same artifact.");
        }

        var exists = await dbContext.ArtifactDependencies.AnyAsync(
            dependency => dependency.TenantId == context.TenantId
                && dependency.DependentVersionId == versionId
                && dependency.RequiredVersionId == request.RequiredVersionId
                && dependency.DependencyKind == request.DependencyKind,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact dependency already exists.");
        }

        var dependency = new ArtifactDependency
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DependentVersionId = versionId,
            RequiredArtifactId = request.RequiredArtifactId,
            RequiredVersionId = request.RequiredVersionId,
            DependencyKind = request.DependencyKind,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ArtifactDependencies.Add(dependency);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await DependenciesQuery(context.TenantId, versionId)
            .SingleAsync(candidate => candidate.Id == dependency.Id, cancellationToken);
    }

    public async Task<ArtifactImpactResponse> GetImpactAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.impact.get", ArtifactPermissions.Read, cancellationToken);
        _ = await RequireVersionAsync(artifactId, versionId, context, "artifacts.impact.get", cancellationToken);

        var dependencies = await DependenciesQuery(context.TenantId, versionId)
            .ToListAsync(cancellationToken);
        var dependents = await dbContext.ArtifactDependencies
            .AsNoTracking()
            .Where(dependency => dependency.TenantId == context.TenantId && dependency.RequiredVersionId == versionId)
            .Join(
                dbContext.ArtifactVersions,
                dependency => dependency.DependentVersionId,
                version => version.Id,
                (dependency, version) => new { dependency, version })
            .Join(
                dbContext.Artifacts,
                pair => pair.version.ArtifactId,
                artifact => artifact.Id,
                (pair, artifact) => new ArtifactDependentResponse(
                    pair.dependency.Id,
                    pair.dependency.TenantId,
                    artifact.Id,
                    artifact.Name,
                    pair.version.Id,
                    pair.version.VersionLabel,
                    pair.dependency.DependencyKind,
                    pair.dependency.CreatedAt))
            .OrderBy(dependent => dependent.DependentArtifactName)
            .ToListAsync(cancellationToken);

        return new ArtifactImpactResponse(dependencies, dependents);
    }

    public async Task<ArtifactReadinessResponse> GetReadinessAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.readiness.get", ArtifactPermissions.Read, cancellationToken);
        var version = await RequireVersionAsync(artifactId, versionId, context, "artifacts.readiness.get", cancellationToken);
        var readiness = await CalculateReadinessAsync(version, cancellationToken);

        return new ArtifactReadinessResponse(
            artifactId,
            versionId,
            version.ReadinessState,
            readiness.State,
            readiness.BlockingReasons,
            version.CompatibilityStatus,
            version.PolicyRiskStatus);
    }

    public async Task<PublishArtifactVersionResult> PublishVersionAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireArtifactPermissionAsync("artifacts.publish", ArtifactPermissions.Publish, cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "artifacts.publish", cancellationToken);
        var version = await RequireVersionAsync(artifactId, versionId, context, "artifacts.publish", cancellationToken);

        if (artifact.OwnerUserId != context.UserId
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Admin, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "artifacts.publish",
                "permission_denied",
                "Only an artifact owner or artifact administrator may publish artifact versions.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks artifact ownership or administration permission.");
        }

        var readiness = await CalculateReadinessAsync(version, cancellationToken);
        if (readiness.BlockingReasons.Count > 0)
        {
            await auditRecorder.RecordAsync(
                new AuditRecordWriteRequest(
                    context.TenantId,
                    context.UserId,
                    "artifacts.publish",
                    AuditResult.Denied,
                    "publish_blocked",
                    $"Artifact version '{version.VersionLabel}' was blocked from publishing.",
                    SourceObjectType: nameof(ArtifactVersion),
                    SourceObjectId: version.Id.ToString(),
                    RetentionCategory: AuditRetentionCategory.Security,
                    IsArchiveEligible: true),
                cancellationToken);

            return new PublishArtifactVersionResult(
                false,
                readiness.State,
                readiness.BlockingReasons,
                version.CompatibilityStatus,
                version.PolicyRiskStatus,
                ToVersionSummary(version));
        }

        version.ReadinessState = ArtifactReadinessState.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.PublishedByUserId = context.UserId;
        version.PublishSummary = TrimOptional(request.Summary);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "artifacts.publish",
                AuditResult.Success,
                null,
                $"Artifact version '{version.VersionLabel}' was published.",
                SourceObjectType: nameof(ArtifactVersion),
                SourceObjectId: version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new PublishArtifactVersionResult(
            true,
            version.ReadinessState,
            [],
            version.CompatibilityStatus,
            version.PolicyRiskStatus,
            ToVersionSummary(version));
    }

    private async Task<ActiveTenantContext> RequireArtifactPermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Admin, cancellationToken);

        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks artifact registry permission.");
        }

        return context;
    }

    private async Task<Artifact> RequireArtifactAsync(
        Guid artifactId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Artifacts.SingleOrDefaultAsync(candidate => candidate.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "artifact_tenant_mismatch",
                "The requested artifact belongs to a different tenant.",
                cancellationToken);
            throw new TenantAccessDeniedException("Artifact is not available in the active tenant.");
        }

        return artifact;
    }

    private async Task<ArtifactVersion> RequireVersionAsync(
        Guid artifactId,
        Guid versionId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        _ = await RequireArtifactAsync(artifactId, context, action, cancellationToken);
        var version = await dbContext.ArtifactVersions.SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        if (version.TenantId != context.TenantId || version.ArtifactId != artifactId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "artifact_tenant_mismatch",
                "The requested artifact version belongs to a different tenant or artifact.",
                cancellationToken);
            throw new TenantAccessDeniedException("Artifact version is not available in the active tenant.");
        }

        return version;
    }

    private async Task<ArtifactDetailResponse> BuildArtifactDetailAsync(Artifact artifact, CancellationToken cancellationToken)
    {
        var versionRows = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.ArtifactId == artifact.Id)
            .OrderByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        var relationships = await RelationshipsQuery(artifact.TenantId, artifact.Id)
            .ToListAsync(cancellationToken);

        return new ArtifactDetailResponse(
            artifact.Id,
            artifact.TenantId,
            artifact.ArtifactType,
            artifact.Name,
            artifact.Description,
            artifact.OwnerUserId,
            artifact.LifecycleState,
            versionRows.Select(ToVersionSummary).ToList(),
            relationships,
            artifact.CreatedAt,
            artifact.UpdatedAt);
    }

    private IQueryable<ArtifactRelationshipResponse> RelationshipsQuery(Guid tenantId, Guid artifactId)
    {
        return dbContext.ArtifactRelationships
            .AsNoTracking()
            .Where(relationship => relationship.TenantId == tenantId && relationship.SourceArtifactId == artifactId)
            .Join(
                dbContext.Artifacts,
                relationship => relationship.TargetArtifactId,
                artifact => artifact.Id,
                (relationship, artifact) => new ArtifactRelationshipResponse(
                    relationship.Id,
                    relationship.TenantId,
                    relationship.SourceArtifactId,
                    relationship.TargetArtifactId,
                    artifact.Name,
                    relationship.RelationshipType,
                    relationship.Description,
                    relationship.CreatedAt))
            .OrderBy(relationship => relationship.TargetArtifactName);
    }

    private IQueryable<ArtifactDependencyResponse> DependenciesQuery(Guid tenantId, Guid versionId)
    {
        return dbContext.ArtifactDependencies
            .AsNoTracking()
            .Where(dependency => dependency.TenantId == tenantId && dependency.DependentVersionId == versionId)
            .Join(
                dbContext.Artifacts,
                dependency => dependency.RequiredArtifactId,
                artifact => artifact.Id,
                (dependency, artifact) => new { dependency, artifact })
            .Join(
                dbContext.ArtifactVersions,
                pair => pair.dependency.RequiredVersionId,
                version => version.Id,
                (pair, version) => new ArtifactDependencyResponse(
                    pair.dependency.Id,
                    pair.dependency.TenantId,
                    pair.dependency.DependentVersionId,
                    pair.dependency.RequiredArtifactId,
                    pair.artifact.Name,
                    pair.dependency.RequiredVersionId,
                    version.VersionLabel,
                    version.ReadinessState,
                    pair.dependency.DependencyKind,
                    pair.dependency.CreatedAt))
            .OrderBy(dependency => dependency.RequiredArtifactName);
    }

    private async Task<(ArtifactReadinessState State, IReadOnlyCollection<string> BlockingReasons)> CalculateReadinessAsync(
        ArtifactVersion version,
        CancellationToken cancellationToken)
    {
        var blockingReasons = new List<string>();

        if (version.ReadinessState is ArtifactReadinessState.Blocked or ArtifactReadinessState.Rejected or ArtifactReadinessState.Retired)
        {
            blockingReasons.Add($"Version readiness is {version.ReadinessState}.");
        }

        if (version.ReadinessState == ArtifactReadinessState.RequiresApproval)
        {
            blockingReasons.Add("Version requires approval before publishing.");
        }

        if (version.ReadinessState == ArtifactReadinessState.Draft)
        {
            blockingReasons.Add("Version must be marked ready before publishing.");
        }

        if (version.CompatibilityStatus == ArtifactCompatibilityStatus.Incompatible)
        {
            blockingReasons.Add("Compatibility status is incompatible.");
        }

        if (version.PolicyRiskStatus is ArtifactPolicyRiskStatus.Blocked or ArtifactPolicyRiskStatus.RequiresApproval)
        {
            blockingReasons.Add($"Policy risk status is {version.PolicyRiskStatus}.");
        }

        var requiredDependencies = await dbContext.ArtifactDependencies
            .AsNoTracking()
            .Where(dependency => dependency.TenantId == version.TenantId
                && dependency.DependentVersionId == version.Id
                && dependency.DependencyKind == ArtifactDependencyKind.DependsOn)
            .Join(
                dbContext.ArtifactVersions,
                dependency => dependency.RequiredVersionId,
                requiredVersion => requiredVersion.Id,
                (dependency, requiredVersion) => new { dependency, requiredVersion })
            .Join(
                dbContext.Artifacts,
                pair => pair.dependency.RequiredArtifactId,
                artifact => artifact.Id,
                (pair, artifact) => new
                {
                    artifact.Name,
                    pair.requiredVersion.VersionLabel,
                    pair.requiredVersion.ReadinessState
                })
            .ToListAsync(cancellationToken);

        foreach (var dependency in requiredDependencies.Where(dependency => dependency.ReadinessState != ArtifactReadinessState.Published))
        {
            blockingReasons.Add(
                $"Required dependency '{dependency.Name}' version '{dependency.VersionLabel}' is {dependency.ReadinessState}.");
        }

        var readinessState = blockingReasons.Count == 0 ? ArtifactReadinessState.Ready : ArtifactReadinessState.Blocked;
        return (readinessState, blockingReasons);
    }

    private static ArtifactSummaryResponse ToArtifactSummary(Artifact artifact, ArtifactVersionSummaryResponse? latestVersion)
    {
        return new ArtifactSummaryResponse(
            artifact.Id,
            artifact.TenantId,
            artifact.ArtifactType,
            artifact.Name,
            artifact.Description,
            artifact.OwnerUserId,
            artifact.LifecycleState,
            latestVersion,
            artifact.CreatedAt,
            artifact.UpdatedAt);
    }

    private static ArtifactVersionSummaryResponse ToVersionSummary(ArtifactVersion version)
    {
        return new ArtifactVersionSummaryResponse(
            version.Id,
            version.TenantId,
            version.ArtifactId,
            version.VersionLabel,
            version.Summary,
            version.ReadinessState,
            version.CompatibilityStatus,
            version.CompatibilitySummary,
            version.PolicyRiskStatus,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt,
            version.PublishSummary);
    }

    private static async Task ValidateAsync<T>(
        IValidator<T> validator,
        T request,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
        }
    }

    private static string NormalizeText(string value) => value.Trim();

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CreateArtifactRequestValidator : AbstractValidator<CreateArtifactRequest>
    {
        public CreateArtifactRequestValidator()
        {
            RuleFor(request => request.ArtifactType).NotEmpty().MaximumLength(120);
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
            RuleFor(request => request.Description).MaximumLength(1000);
        }
    }

    private sealed class CreateArtifactVersionRequestValidator : AbstractValidator<CreateArtifactVersionRequest>
    {
        public CreateArtifactVersionRequestValidator()
        {
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.PayloadJson).MaximumLength(8000);
            RuleFor(request => request.CompatibilitySummary).MaximumLength(1000);
            RuleFor(request => request.ReadinessState)
                .Must(state => state != ArtifactReadinessState.Published)
                .WithMessage("New artifact versions cannot be created as published.");
        }
    }

    private sealed class CreateArtifactRelationshipRequestValidator : AbstractValidator<CreateArtifactRelationshipRequest>
    {
        public CreateArtifactRelationshipRequestValidator()
        {
            RuleFor(request => request.TargetArtifactId).NotEmpty();
            RuleFor(request => request.Description).MaximumLength(1000);
        }
    }

    private sealed class CreateArtifactDependencyRequestValidator : AbstractValidator<CreateArtifactDependencyRequest>
    {
        public CreateArtifactDependencyRequestValidator()
        {
            RuleFor(request => request.RequiredArtifactId).NotEmpty();
            RuleFor(request => request.RequiredVersionId).NotEmpty();
        }
    }
}
