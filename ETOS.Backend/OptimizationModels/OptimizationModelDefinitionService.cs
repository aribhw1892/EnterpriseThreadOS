using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.OptimizationModels;

public interface IOptimizationModelDefinitionService
{
    Task<IReadOnlyCollection<OptimizationModelDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<OptimizationModelDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<OptimizationModelDependencySummaryResponse> GetDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateOptimizationModelDefinitionResponse> CreateAsync(CreateOptimizationModelDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateOptimizationModelDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateOptimizationModelDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkOptimizationModelDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishOptimizationModelDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class OptimizationModelDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService) : IOptimizationModelDefinitionService
{
    public async Task<IReadOnlyCollection<OptimizationModelDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("optimization-models.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("optimization-models.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == OptimizationModelDefinitionArtifactTypes.OptimizationModel.ToUpperInvariant())
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
            string? optimizationKey = null;
            string? objectiveCategory = null;
            if (version?.PayloadJson is not null)
            {
                var payload = OptimizationModelDefinitionPayloadParser.Deserialize(version.PayloadJson);
                optimizationKey = payload.OptimizationKey;
                objectiveCategory = payload.ObjectiveCategory;
            }

            return new OptimizationModelDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                optimizationKey,
                objectiveCategory,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<OptimizationModelDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "optimization-models.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("optimization-models.get", cancellationToken);
        var document = OptimizationModelDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);

        return OptimizationModelDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.ModelPackages,
            dependencies.Ontologies);
    }

    public async Task<OptimizationModelDependencySummaryResponse> GetDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "optimization-models.dependencies.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("optimization-models.dependencies.get", cancellationToken);
        var document = OptimizationModelDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);
        return new OptimizationModelDependencySummaryResponse(
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.ModelPackages,
            dependencies.Ontologies);
    }

    public async Task<CreateOptimizationModelDefinitionResponse> CreateAsync(
        CreateOptimizationModelDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = OptimizationModelDefinitionPayloadParser.Create(
            request.OptimizationKey,
            request.ObjectiveCategory,
            request.ObjectiveSummary,
            request.ObjectiveMetadata,
            request.SolverConfiguration,
            request.InputRequirements,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.FutureExtensionPlaceholders);
        OptimizationModelDefinitionPayloadParser.ValidateCore(payload);

        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = OptimizationModelDefinitionArtifactTypes.OptimizationModel,
            NormalizedArtifactType = OptimizationModelDefinitionArtifactTypes.OptimizationModel.ToUpperInvariant(),
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
            Summary = TrimOptional(request.ObjectiveSummary),
            PayloadJson = OptimizationModelDefinitionPayloadParser.Serialize(payload),
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
                "optimization-models.create",
                AuditResult.Success,
                null,
                $"Optimization model definition '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateOptimizationModelDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateOptimizationModelDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateOptimizationModelDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "optimization-models.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = OptimizationModelDefinitionPayloadParser.Create(
            request.OptimizationKey,
            request.ObjectiveCategory,
            request.ObjectiveSummary,
            request.ObjectiveMetadata,
            request.SolverConfiguration,
            request.InputRequirements,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.FutureExtensionPlaceholders);
        OptimizationModelDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary ?? request.ObjectiveSummary),
            PayloadJson = OptimizationModelDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
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
                "optimization-models.versions.create",
                AuditResult.Success,
                null,
                $"Optimization model definition version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateOptimizationModelDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkOptimizationModelDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("optimization-models.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "optimization-models.readiness.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "optimization-models.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = OptimizationModelDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var validationNotes = await OptimizationModelDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
            dbContext,
            context.TenantId,
            document,
            cancellationToken);
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

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "optimization-models.readiness.mark",
                AuditResult.Success,
                null,
                $"Optimization model definition version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkOptimizationModelDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<PublishOptimizationModelDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "optimization-models.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishOptimizationModelDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    private async Task<(
        IReadOnlyCollection<OptimizationModelCapabilityReferenceResponse> Capabilities,
        IReadOnlyCollection<OptimizationModelBusinessPolicyReferenceResponse> BusinessPolicies,
        IReadOnlyCollection<OptimizationModelPackageReferenceResponse> ModelPackages,
        IReadOnlyCollection<OptimizationModelOntologyReferenceResponse> Ontologies)> ResolveDependenciesAsync(
        Guid tenantId,
        OptimizationModelDefinitionPayloadParser.OptimizationModelDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var capabilityVersionIds = document.ReferencedCapabilityDefinitionVersionIds ?? [];
        var policyVersionIds = document.ReferencedBusinessPolicyDefinitionVersionIds ?? [];
        var packageIds = document.CompatibleModelPackageVersionIds ?? [];
        var ontologyIds = document.CompatibleOntologyVersionIds ?? [];

        var capabilities = capabilityVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && capabilityVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == CapabilityDefinitionArtifactTypes.CapabilityDefinition
                select new OptimizationModelCapabilityReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractCapabilityKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var businessPolicies = policyVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && policyVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition
                select new OptimizationModelBusinessPolicyReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractPolicyKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var packages = packageIds.Count == 0
            ? []
            : await dbContext.ModelPackageVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && packageIds.Contains(item.Id))
                .Select(item => new OptimizationModelPackageReferenceResponse(
                    item.Id,
                    item.Key,
                    item.Name,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        var ontologies = ontologyIds.Count == 0
            ? []
            : await dbContext.OntologyVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && ontologyIds.Contains(item.Id))
                .Select(item => new OptimizationModelOntologyReferenceResponse(
                    item.Id,
                    item.Key,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        return (capabilities, businessPolicies, packages, ontologies);
    }

    private static string ExtractCapabilityKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            var document = CapabilityDefinitionPayloadParser.Deserialize(payloadJson);
            return document.CapabilityKey ?? string.Empty;
        }
        catch (RequestValidationException)
        {
            return string.Empty;
        }
    }

    private static string ExtractPolicyKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            var document = BusinessPolicyDefinitionPayloadParser.Deserialize(payloadJson);
            return document.PolicyKey ?? string.Empty;
        }
        catch (RequestValidationException)
        {
            return string.Empty;
        }
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

        if (!artifact.ArtifactType.Equals(
                OptimizationModelDefinitionArtifactTypes.OptimizationModel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{OptimizationModelDefinitionArtifactTypes.OptimizationModel}'.");
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

        if (!artifact.ArtifactType.Equals(
                OptimizationModelDefinitionArtifactTypes.OptimizationModel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{OptimizationModelDefinitionArtifactTypes.OptimizationModel}'.");
        }

        return artifact;
    }

    private async Task RequireOwnerOrAdminAsync(
        ActiveTenantContext context,
        Artifact artifact,
        string action,
        CancellationToken cancellationToken)
    {
        if (artifact.OwnerUserId == context.UserId
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OptimizationModelDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            "Only an artifact owner or optimization model administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks artifact ownership or optimization model administration permission.");
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
            $"The user lacks the {OptimizationModelDefinitionPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks optimization model definition read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("optimization-models.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "optimization-models.create",
            "permission_denied",
            $"The user lacks the {OptimizationModelDefinitionPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks optimization model definition create permission.");
    }

    private async Task<ActiveTenantContext> RequireReadinessPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await HasReadinessPermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {OptimizationModelDefinitionPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks optimization model definition readiness permission.");
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "tenant_access_denied",
            "Record belongs to a different tenant.",
            cancellationToken);
        throw new TenantAccessDeniedException("Record is not available in the active tenant.");
    }

    private async Task<bool> HasReadPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OptimizationModelDefinitionPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OptimizationModelDefinitionPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OptimizationModelDefinitionPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OptimizationModelDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
