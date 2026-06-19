using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using ETOS.Backend.Platform.JsonSchema;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public interface IToolDefinitionService
{
    Task<IReadOnlyCollection<ToolDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ToolDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<ToolDependencySummaryResponse> GetDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateToolDefinitionResponse> CreateAsync(CreateToolDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateToolDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateToolDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkToolDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishToolDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
    Task<ToolCompatibilityScanResponse> CompatibilityScanAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken);
    Task<ToolExecutionResponse> DryRunAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken);
    Task<ToolExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ToolDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService,
    IJsonSchemaValidator jsonSchemaValidator,
    IToolGateway toolGateway) : IToolDefinitionService
{
    public async Task<IReadOnlyCollection<ToolDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("tools.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("tools.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == ToolDefinitionArtifactTypes.ToolDefinition.ToUpperInvariant())
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
            string? toolKey = null;
            string? toolCategory = null;
            string? riskLevel = null;
            if (version?.PayloadJson is not null)
            {
                var payload = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson);
                toolKey = payload.ToolKey;
                toolCategory = payload.ToolCategory;
                riskLevel = payload.RiskLevel;
            }

            return new ToolDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                toolKey,
                toolCategory,
                riskLevel,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<ToolDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "tools.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("tools.get", cancellationToken);
        var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);

        return ToolDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            dependencies);
    }

    public async Task<ToolDependencySummaryResponse> GetDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "tools.dependencies.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("tools.dependencies.get", cancellationToken);
        var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        return await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);
    }

    public async Task<CreateToolDefinitionResponse> CreateAsync(
        CreateToolDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request);
        ToolDefinitionPayloadParser.ValidateCore(payload);

        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = ToolDefinitionArtifactTypes.ToolDefinition,
            NormalizedArtifactType = ToolDefinitionArtifactTypes.ToolDefinition.ToUpperInvariant(),
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
            PayloadJson = ToolDefinitionPayloadParser.Serialize(payload),
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
                "tools.create",
                AuditResult.Success,
                null,
                $"Tool definition '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateToolDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateToolDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateToolDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "tools.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request);
        ToolDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            PayloadJson = ToolDefinitionPayloadParser.Serialize(payload),
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
                "tools.versions.create",
                AuditResult.Success,
                null,
                $"Tool definition version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateToolDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkToolDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("tools.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "tools.readiness.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "tools.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var validationNotes = await ToolDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
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
                "tools.readiness.mark",
                AuditResult.Success,
                null,
                $"Tool definition version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkToolDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<PublishToolDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "tools.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishToolDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    public async Task<ToolCompatibilityScanResponse> CompatibilityScanAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "tools.compatibility_scan", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("tools.compatibility_scan", cancellationToken);
        var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var notes = await ToolDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
            dbContext,
            jsonSchemaValidator,
            context.TenantId,
            document,
            cancellationToken);

        return new ToolCompatibilityScanResponse(
            artifactId,
            versionId,
            notes.Count == 0,
            notes.ToList());
    }

    public Task<ToolExecutionResponse> DryRunAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken)
        => toolGateway.DryRunAsync(artifactId, versionId, request, cancellationToken);

    public Task<ToolExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken)
        => toolGateway.ExecuteAsync(artifactId, versionId, request, cancellationToken);

    private static ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument BuildPayload(CreateToolDefinitionRequest request)
        => ToolDefinitionPayloadParser.Create(
            request.ToolKey,
            request.ToolCategory,
            request.RiskLevel,
            request.ReadOnly,
            request.CreatesPlatformArtifact,
            request.CreatesReviewTask,
            request.CreatesDecision,
            request.CallsExternalSystem,
            request.WritesExternalSystem,
            request.RequiresApproval,
            request.SupportsDryRun,
            request.RequiredPermissionKeys,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.InternalHandlerKey,
            request.ReferencedOutputSchemaVersionId,
            request.ConnectorDefinitionVersionId,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.AllowedQueryIntentKeys,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private static ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument BuildPayload(CreateToolDefinitionVersionRequest request)
        => ToolDefinitionPayloadParser.Create(
            request.ToolKey,
            request.ToolCategory,
            request.RiskLevel,
            request.ReadOnly,
            request.CreatesPlatformArtifact,
            request.CreatesReviewTask,
            request.CreatesDecision,
            request.CallsExternalSystem,
            request.WritesExternalSystem,
            request.RequiresApproval,
            request.SupportsDryRun,
            request.RequiredPermissionKeys,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.InternalHandlerKey,
            request.ReferencedOutputSchemaVersionId,
            request.ConnectorDefinitionVersionId,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.AllowedQueryIntentKeys,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private async Task<ToolDependencySummaryResponse> ResolveDependenciesAsync(
        Guid tenantId,
        ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var packageIds = document.CompatibleModelPackageVersionIds ?? [];
        var ontologyIds = document.CompatibleOntologyVersionIds ?? [];
        var capabilityVersionIds = document.ReferencedCapabilityDefinitionVersionIds ?? [];
        var policyVersionIds = document.ReferencedBusinessPolicyDefinitionVersionIds ?? [];

        var packages = packageIds.Count == 0
            ? []
            : await dbContext.ModelPackageVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && packageIds.Contains(item.Id))
                .Select(item => new ToolModelPackageReferenceResponse(
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
                .Select(item => new ToolOntologyReferenceResponse(
                    item.Id,
                    item.Key,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        var capabilities = capabilityVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && capabilityVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == CapabilityDefinitionArtifactTypes.CapabilityDefinition
                select new ToolCapabilityReferenceResponse(
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
                select new ToolBusinessPolicyReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractPolicyKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        ToolOutputSchemaReferenceResponse? outputSchema = null;
        if (document.ReferencedOutputSchemaVersionId is Guid outputSchemaVersionId)
        {
            outputSchema = await ResolveOutputSchemaReferenceAsync(tenantId, outputSchemaVersionId, cancellationToken);
        }

        ToolConnectorReferenceResponse? connector = null;
        if (document.ConnectorDefinitionVersionId is Guid connectorVersionId)
        {
            connector = await ResolveConnectorReferenceAsync(tenantId, connectorVersionId, cancellationToken);
        }

        return new ToolDependencySummaryResponse(
            packages,
            ontologies,
            capabilities,
            businessPolicies,
            outputSchema,
            connector);
    }

    private async Task<ToolOutputSchemaReferenceResponse?> ResolveOutputSchemaReferenceAsync(
        Guid tenantId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId && item.TenantId == tenantId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == version.ArtifactId, cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        return new ToolOutputSchemaReferenceResponse(
            version.Id,
            artifact.Id,
            artifact.Name,
            version.VersionLabel,
            version.ReadinessState.ToString());
    }

    private async Task<ToolConnectorReferenceResponse?> ResolveConnectorReferenceAsync(
        Guid tenantId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId && item.TenantId == tenantId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == version.ArtifactId, cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        return new ToolConnectorReferenceResponse(
            version.Id,
            artifact.Id,
            artifact.Name,
            ExtractConnectorKey(version.PayloadJson),
            version.VersionLabel,
            version.ReadinessState.ToString());
    }

    private static string ExtractCapabilityKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return CapabilityDefinitionPayloadParser.Deserialize(payloadJson).CapabilityKey ?? string.Empty;
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
            return BusinessPolicyDefinitionPayloadParser.Deserialize(payloadJson).PolicyKey ?? string.Empty;
        }
        catch (RequestValidationException)
        {
            return string.Empty;
        }
    }

    private static string ExtractConnectorKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return ConnectorDefinitionPayloadParser.Deserialize(payloadJson).ConnectorKey ?? string.Empty;
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

        if (!artifact.ArtifactType.Equals(ToolDefinitionArtifactTypes.ToolDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{ToolDefinitionArtifactTypes.ToolDefinition}'.");
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

        if (!artifact.ArtifactType.Equals(ToolDefinitionArtifactTypes.ToolDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{ToolDefinitionArtifactTypes.ToolDefinition}'.");
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
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Admin, cancellationToken)
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
            "Only an artifact owner or tool administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks artifact ownership or tool administration permission.");
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
            $"The user lacks the {ToolDefinitionPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks tool definition read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("tools.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "tools.create",
            "permission_denied",
            $"The user lacks the {ToolDefinitionPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks tool definition create permission.");
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
            $"The user lacks the {ToolDefinitionPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks tool definition readiness permission.");
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
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
