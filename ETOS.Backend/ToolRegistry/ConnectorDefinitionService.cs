using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public interface IConnectorDefinitionService
{
    Task<IReadOnlyCollection<ConnectorDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ConnectorDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateConnectorDefinitionResponse> CreateAsync(CreateConnectorDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateConnectorDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateConnectorDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkConnectorDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishConnectorDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ConnectorDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService) : IConnectorDefinitionService
{
    public async Task<IReadOnlyCollection<ConnectorDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("connectors.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("connectors.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == ConnectorDefinitionArtifactTypes.ConnectorDefinition.ToUpperInvariant())
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
            string? connectorKey = null;
            string? connectorKind = null;
            bool? executionEnabled = null;
            if (version?.PayloadJson is not null)
            {
                var payload = ConnectorDefinitionPayloadParser.Deserialize(version.PayloadJson);
                connectorKey = payload.ConnectorKey;
                connectorKind = payload.ConnectorKind;
                executionEnabled = payload.ExecutionEnabled;
            }

            return new ConnectorDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                connectorKey,
                connectorKind,
                executionEnabled,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<ConnectorDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "connectors.get", cancellationToken);

        return ConnectorDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}");
    }

    public async Task<CreateConnectorDefinitionResponse> CreateAsync(
        CreateConnectorDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request);
        ConnectorDefinitionPayloadParser.ValidateCore(payload);

        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = ConnectorDefinitionArtifactTypes.ConnectorDefinition,
            NormalizedArtifactType = ConnectorDefinitionArtifactTypes.ConnectorDefinition.ToUpperInvariant(),
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
            Summary = TrimOptional(request.Description),
            PayloadJson = ConnectorDefinitionPayloadParser.Serialize(payload),
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
                "connectors.create",
                AuditResult.Success,
                null,
                $"Connector definition '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateConnectorDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateConnectorDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateConnectorDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "connectors.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request);
        ConnectorDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            PayloadJson = ConnectorDefinitionPayloadParser.Serialize(payload),
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
                "connectors.versions.create",
                AuditResult.Success,
                null,
                $"Connector definition version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateConnectorDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkConnectorDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("connectors.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "connectors.readiness.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "connectors.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = ConnectorDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var validationNotes = ConnectorDefinitionReadinessValidator.ValidateRequiredFields(document);
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
                "connectors.readiness.mark",
                AuditResult.Success,
                null,
                $"Connector definition version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkConnectorDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<PublishConnectorDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "connectors.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishConnectorDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    private static ConnectorDefinitionPayloadParser.ConnectorDefinitionPayloadDocument BuildPayload(
        CreateConnectorDefinitionRequest request)
        => ConnectorDefinitionPayloadParser.Create(
            request.ConnectorKey,
            request.ConnectorKind,
            request.CallsExternalSystem,
            request.WritesExternalSystem,
            request.ExecutionEnabled,
            request.DisabledReason,
            request.CredentialScopeKey,
            request.SecretReferenceKey,
            request.SupportedOperations,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private static ConnectorDefinitionPayloadParser.ConnectorDefinitionPayloadDocument BuildPayload(
        CreateConnectorDefinitionVersionRequest request)
        => ConnectorDefinitionPayloadParser.Create(
            request.ConnectorKey,
            request.ConnectorKind,
            request.CallsExternalSystem,
            request.WritesExternalSystem,
            request.ExecutionEnabled,
            request.DisabledReason,
            request.CredentialScopeKey,
            request.SecretReferenceKey,
            request.SupportedOperations,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

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

        if (!artifact.ArtifactType.Equals(ConnectorDefinitionArtifactTypes.ConnectorDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{ConnectorDefinitionArtifactTypes.ConnectorDefinition}'.");
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

        if (!artifact.ArtifactType.Equals(ConnectorDefinitionArtifactTypes.ConnectorDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{ConnectorDefinitionArtifactTypes.ConnectorDefinition}'.");
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
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ConnectorDefinitionPermissions.Admin, cancellationToken)
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
            "Only an artifact owner or connector administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks artifact ownership or connector administration permission.");
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
            $"The user lacks the {ConnectorDefinitionPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks connector definition read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("connectors.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "connectors.create",
            "permission_denied",
            $"The user lacks the {ConnectorDefinitionPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks connector definition create permission.");
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
            $"The user lacks the {ConnectorDefinitionPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks connector definition readiness permission.");
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
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ConnectorDefinitionPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ConnectorDefinitionPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ConnectorDefinitionPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ConnectorDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
