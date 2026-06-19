using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Platform.JsonSchema;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public interface ISkillDefinitionService
{
    Task<IReadOnlyCollection<SkillDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<SkillDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<SkillDependencySummaryResponse> GetDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateSkillDefinitionResponse> CreateAsync(CreateSkillDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateSkillDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateSkillDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkSkillDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishSkillDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class SkillDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService,
    IJsonSchemaValidator jsonSchemaValidator) : ISkillDefinitionService
{
    public async Task<IReadOnlyCollection<SkillDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("skills.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("skills.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == SkillDefinitionArtifactTypes.SkillDefinition.ToUpperInvariant())
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
            string? skillKey = null;
            bool isGloballyShared = false;
            if (version?.PayloadJson is not null)
            {
                var payload = SkillDefinitionPayloadParser.Deserialize(version.PayloadJson);
                skillKey = payload.SkillKey;
                isGloballyShared = payload.IsGloballyShared;
            }

            return new SkillDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                skillKey,
                isGloballyShared,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<SkillDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "skills.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("skills.get", cancellationToken);
        var document = SkillDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var referencedTools = await ResolveToolReferencesAsync(context.TenantId, document, cancellationToken);

        return SkillDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            referencedTools);
    }

    public async Task<SkillDependencySummaryResponse> GetDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "skills.dependencies.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("skills.dependencies.get", cancellationToken);
        var document = SkillDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var tools = await ResolveToolReferencesAsync(context.TenantId, document, cancellationToken);
        return new SkillDependencySummaryResponse(tools);
    }

    public async Task<CreateSkillDefinitionResponse> CreateAsync(
        CreateSkillDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request);
        SkillDefinitionPayloadParser.ValidateCore(payload);

        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = SkillDefinitionArtifactTypes.SkillDefinition,
            NormalizedArtifactType = SkillDefinitionArtifactTypes.SkillDefinition.ToUpperInvariant(),
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
            Summary = TrimOptional(request.SkillSummary),
            PayloadJson = SkillDefinitionPayloadParser.Serialize(payload),
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
                "skills.create",
                AuditResult.Success,
                null,
                $"Skill definition '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateSkillDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateSkillDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateSkillDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "skills.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request);
        SkillDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary ?? request.SkillSummary),
            PayloadJson = SkillDefinitionPayloadParser.Serialize(payload),
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
                "skills.versions.create",
                AuditResult.Success,
                null,
                $"Skill definition version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateSkillDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkSkillDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("skills.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "skills.readiness.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "skills.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = SkillDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var validationNotes = await SkillDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
            dbContext,
            jsonSchemaValidator,
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
                "skills.readiness.mark",
                AuditResult.Success,
                null,
                $"Skill definition version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkSkillDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<PublishSkillDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "skills.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishSkillDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    private static SkillDefinitionPayloadParser.SkillDefinitionPayloadDocument BuildPayload(CreateSkillDefinitionRequest request)
        => SkillDefinitionPayloadParser.Create(
            request.SkillKey,
            request.SkillSummary,
            request.IsGloballyShared,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.ReferencedToolDefinitionVersionIds,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private static SkillDefinitionPayloadParser.SkillDefinitionPayloadDocument BuildPayload(CreateSkillDefinitionVersionRequest request)
        => SkillDefinitionPayloadParser.Create(
            request.SkillKey,
            request.SkillSummary,
            request.IsGloballyShared,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.ReferencedToolDefinitionVersionIds,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private async Task<IReadOnlyCollection<SkillToolReferenceResponse>> ResolveToolReferencesAsync(
        Guid tenantId,
        SkillDefinitionPayloadParser.SkillDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var toolVersionIds = document.ReferencedToolDefinitionVersionIds ?? [];
        if (toolVersionIds.Count == 0)
        {
            return [];
        }

        return await (
            from version in dbContext.ArtifactVersions.AsNoTracking()
            join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
            where version.TenantId == tenantId
                && toolVersionIds.Contains(version.Id)
                && artifact.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition
            select new SkillToolReferenceResponse(
                version.Id,
                artifact.Id,
                artifact.Name,
                ExtractToolKey(version.PayloadJson),
                version.VersionLabel,
                version.ReadinessState.ToString()))
            .ToListAsync(cancellationToken);
    }

    private static string ExtractToolKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return ToolDefinitionPayloadParser.Deserialize(payloadJson).ToolKey ?? string.Empty;
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

        if (!artifact.ArtifactType.Equals(SkillDefinitionArtifactTypes.SkillDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{SkillDefinitionArtifactTypes.SkillDefinition}'.");
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

        if (!artifact.ArtifactType.Equals(SkillDefinitionArtifactTypes.SkillDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{SkillDefinitionArtifactTypes.SkillDefinition}'.");
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
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, SkillDefinitionPermissions.Admin, cancellationToken)
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
            "Only an artifact owner or skill administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks artifact ownership or skill administration permission.");
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
            $"The user lacks the {SkillDefinitionPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks skill definition read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("skills.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "skills.create",
            "permission_denied",
            $"The user lacks the {SkillDefinitionPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks skill definition create permission.");
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
            $"The user lacks the {SkillDefinitionPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks skill definition readiness permission.");
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
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, SkillDefinitionPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, SkillDefinitionPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, SkillDefinitionPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, SkillDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
