using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AgentTemplates;

public interface IAgentTemplateDefinitionService
{
    Task<IReadOnlyCollection<AgentTemplateDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AgentTemplateDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<AgentTemplateDependencySummaryResponse> GetDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateAgentTemplateDefinitionResponse> CreateAsync(CreateAgentTemplateDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateAgentTemplateDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateAgentTemplateDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkAgentTemplateDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishAgentTemplateDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class AgentTemplateDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService) : IAgentTemplateDefinitionService
{
    public async Task<IReadOnlyCollection<AgentTemplateDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("agent-templates.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agent-templates.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant())
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
            string? patternCategory = null;
            if (version?.PayloadJson is not null)
            {
                var payload = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson);
                templateKey = payload.TemplateKey;
                patternCategory = payload.PatternCategory;
            }

            return new AgentTemplateDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                templateKey,
                patternCategory,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<AgentTemplateDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "agent-templates.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agent-templates.get", cancellationToken);
        var document = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);

        return AgentTemplateDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.OptimizationModels,
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.PromptTemplate,
            dependencies.OutputSchema,
            dependencies.QueryIntent,
            dependencies.RetrievalStrategy,
            dependencies.Tools);
    }

    public async Task<AgentTemplateDependencySummaryResponse> GetDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "agent-templates.dependencies.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agent-templates.dependencies.get", cancellationToken);
        var document = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);
        return new AgentTemplateDependencySummaryResponse(
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.OptimizationModels,
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.PromptTemplate,
            dependencies.OutputSchema,
            dependencies.QueryIntent,
            dependencies.RetrievalStrategy,
            dependencies.Tools);
    }

    public async Task<CreateAgentTemplateDefinitionResponse> CreateAsync(
        CreateAgentTemplateDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request);
        AgentTemplateDefinitionPayloadParser.ValidateCore(payload);

        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = AgentTemplateDefinitionArtifactTypes.AgentTemplate,
            NormalizedArtifactType = AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant(),
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
            Summary = TrimOptional(request.PatternSummary),
            PayloadJson = AgentTemplateDefinitionPayloadParser.Serialize(payload),
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
                "agent-templates.create",
                AuditResult.Success,
                null,
                $"Agent template definition '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateAgentTemplateDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateAgentTemplateDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateAgentTemplateDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "agent-templates.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request);
        AgentTemplateDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary ?? request.PatternSummary),
            PayloadJson = AgentTemplateDefinitionPayloadParser.Serialize(payload),
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
                "agent-templates.versions.create",
                AuditResult.Success,
                null,
                $"Agent template definition version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateAgentTemplateDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkAgentTemplateDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("agent-templates.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "agent-templates.readiness.mark", cancellationToken);
        await RequireOwnerOrAdminAsync(context, artifact, "agent-templates.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var validationNotes = await AgentTemplateDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
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
                "agent-templates.readiness.mark",
                AuditResult.Success,
                null,
                $"Agent template definition version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkAgentTemplateDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<PublishAgentTemplateDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "agent-templates.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishAgentTemplateDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    private static AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument BuildPayload(
        CreateAgentTemplateDefinitionRequest request)
        => AgentTemplateDefinitionPayloadParser.Create(
            request.TemplateKey,
            request.PatternCategory,
            request.PatternSummary,
            request.PreferredRuntimeAdapterKey,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.ReferencedOptimizationModelVersionIds,
            request.PromptTemplateVersionId,
            request.OutputSchemaVersionId,
            request.QueryIntentVersionId,
            request.RetrievalStrategyVersionId,
            request.ReferencedToolDefinitionVersionIds,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private static AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument BuildPayload(
        CreateAgentTemplateDefinitionVersionRequest request)
        => AgentTemplateDefinitionPayloadParser.Create(
            request.TemplateKey,
            request.PatternCategory,
            request.PatternSummary,
            request.PreferredRuntimeAdapterKey,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.ReferencedOptimizationModelVersionIds,
            request.PromptTemplateVersionId,
            request.OutputSchemaVersionId,
            request.QueryIntentVersionId,
            request.RetrievalStrategyVersionId,
            request.ReferencedToolDefinitionVersionIds,
            request.CompositionMetadata,
            request.FutureExtensionPlaceholders);

    private async Task<ResolvedDependencies> ResolveDependenciesAsync(
        Guid tenantId,
        AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var capabilityVersionIds = document.ReferencedCapabilityDefinitionVersionIds ?? [];
        var policyVersionIds = document.ReferencedBusinessPolicyDefinitionVersionIds ?? [];
        var optimizationVersionIds = document.ReferencedOptimizationModelVersionIds ?? [];
        var toolVersionIds = document.ReferencedToolDefinitionVersionIds ?? [];
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
                select new AgentTemplateCapabilityReferenceResponse(
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
                select new AgentTemplateBusinessPolicyReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractPolicyKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var optimizationModels = optimizationVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && optimizationVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == OptimizationModelDefinitionArtifactTypes.OptimizationModel
                select new AgentTemplateOptimizationModelReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractOptimizationKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var tools = toolVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && toolVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition
                select new AgentTemplateToolReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var packages = packageIds.Count == 0
            ? []
            : await dbContext.ModelPackageVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && packageIds.Contains(item.Id))
                .Select(item => new AgentTemplateModelPackageReferenceResponse(
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
                .Select(item => new AgentTemplateOntologyReferenceResponse(
                    item.Id,
                    item.Key,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        var promptTemplate = document.PromptTemplateVersionId is Guid promptId
            ? await ResolveArtifactVersionReferenceAsync(tenantId, promptId, cancellationToken)
            : null;
        var outputSchema = document.OutputSchemaVersionId is Guid outputId
            ? await ResolveArtifactVersionReferenceAsync(tenantId, outputId, cancellationToken)
            : null;

        AgentTemplateQueryIntentReferenceResponse? queryIntent = null;
        if (document.QueryIntentVersionId is Guid queryIntentId)
        {
            var intent = await dbContext.QueryIntentVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == queryIntentId && item.TenantId == tenantId, cancellationToken);
            if (intent is not null)
            {
                queryIntent = new AgentTemplateQueryIntentReferenceResponse(
                    intent.Id,
                    intent.IntentKey,
                    intent.VersionLabel,
                    intent.IsEnabled);
            }
        }

        AgentTemplateRetrievalStrategyReferenceResponse? retrievalStrategy = null;
        if (document.RetrievalStrategyVersionId is Guid retrievalStrategyId)
        {
            var strategy = await dbContext.RetrievalStrategyVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == retrievalStrategyId && item.TenantId == tenantId, cancellationToken);
            if (strategy is not null)
            {
                retrievalStrategy = new AgentTemplateRetrievalStrategyReferenceResponse(
                    strategy.Id,
                    strategy.StrategyKey,
                    strategy.VersionLabel,
                    strategy.IsEnabled);
            }
        }

        return new ResolvedDependencies(
            capabilities,
            businessPolicies,
            optimizationModels,
            packages,
            ontologies,
            promptTemplate,
            outputSchema,
            queryIntent,
            retrievalStrategy,
            tools);
    }

    private async Task<AgentTemplateArtifactVersionReferenceResponse?> ResolveArtifactVersionReferenceAsync(
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

        return new AgentTemplateArtifactVersionReferenceResponse(
            version.Id,
            artifact.Id,
            artifact.ArtifactType,
            artifact.Name,
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

    private static string ExtractOptimizationKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return OptimizationModelDefinitionPayloadParser.Deserialize(payloadJson).OptimizationKey ?? string.Empty;
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
                AgentTemplateDefinitionArtifactTypes.AgentTemplate,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{AgentTemplateDefinitionArtifactTypes.AgentTemplate}'.");
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
                AgentTemplateDefinitionArtifactTypes.AgentTemplate,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{AgentTemplateDefinitionArtifactTypes.AgentTemplate}'.");
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
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentTemplateDefinitionPermissions.Admin, cancellationToken)
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
            "Only an artifact owner or agent template administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks artifact ownership or agent template administration permission.");
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
            $"The user lacks the {AgentTemplateDefinitionPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent template definition read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("agent-templates.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "agent-templates.create",
            "permission_denied",
            $"The user lacks the {AgentTemplateDefinitionPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent template definition create permission.");
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
            $"The user lacks the {AgentTemplateDefinitionPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent template definition readiness permission.");
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
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentTemplateDefinitionPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentTemplateDefinitionPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentTemplateDefinitionPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentTemplateDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ResolvedDependencies(
        IReadOnlyCollection<AgentTemplateCapabilityReferenceResponse> Capabilities,
        IReadOnlyCollection<AgentTemplateBusinessPolicyReferenceResponse> BusinessPolicies,
        IReadOnlyCollection<AgentTemplateOptimizationModelReferenceResponse> OptimizationModels,
        IReadOnlyCollection<AgentTemplateModelPackageReferenceResponse> ModelPackages,
        IReadOnlyCollection<AgentTemplateOntologyReferenceResponse> Ontologies,
        AgentTemplateArtifactVersionReferenceResponse? PromptTemplate,
        AgentTemplateArtifactVersionReferenceResponse? OutputSchema,
        AgentTemplateQueryIntentReferenceResponse? QueryIntent,
        AgentTemplateRetrievalStrategyReferenceResponse? RetrievalStrategy,
        IReadOnlyCollection<AgentTemplateToolReferenceResponse> Tools);
}
