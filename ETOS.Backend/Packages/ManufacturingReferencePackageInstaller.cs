using System.Text.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Packages;

public interface IReferencePackageInstaller
{
    Task<InstallReferencePackageResponse> InstallAsync(InstallReferencePackageRequest request, CancellationToken cancellationToken);
}

public sealed class ManufacturingReferencePackageInstaller(
    IReferencePackageManifestLoader manifestLoader,
    IOntologyService ontologyService,
    ICapabilityDefinitionService capabilityDefinitionService,
    IBusinessPolicyDefinitionService businessPolicyDefinitionService,
    IOptimizationModelDefinitionService optimizationModelDefinitionService,
    IAgentTemplateDefinitionService agentTemplateDefinitionService,
    IConnectorDefinitionService connectorDefinitionService,
    IToolDefinitionService toolDefinitionService,
    ISkillDefinitionService skillDefinitionService,
    IGovernedChatArtifactSeeder governedChatArtifactSeeder,
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder) : IReferencePackageInstaller
{
    private const string InstallAction = "reference-package.install";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InstallReferencePackageResponse> InstallAsync(
        InstallReferencePackageRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.PackageKey, ManufacturingReferencePackageKeys.PackageKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"Unsupported reference package key '{request.PackageKey}'.");
        }

        await RequireWildcardPermissionAsync(cancellationToken);
        var context = await tenantContextResolver.ResolveAsync(InstallAction, cancellationToken);
        var loaded = manifestLoader.Load(request.PackageKey);

        var existing = await ontologyService.GetActiveModelPackageAsync(loaded.Manifest.PackageKey, cancellationToken);
        if (existing is not null)
        {
            var artifacts = await LoadInstalledArtifactSummariesAsync(context.TenantId, cancellationToken);
            return new InstallReferencePackageResponse(
                loaded.Manifest.PackageKey,
                true,
                existing,
                artifacts,
                $"Reference package '{loaded.Manifest.PackageKey}' is already published for this tenant.");
        }

        var modelPackage = await InstallOntologyStackAsync(loaded, cancellationToken);
        var installedArtifacts = new List<InstalledReferenceArtifactResponse>();
        var capabilityVersions = await InstallCapabilitiesAsync(loaded, modelPackage.Id, installedArtifacts, cancellationToken);
        var policyVersions = await InstallBusinessPoliciesAsync(loaded, modelPackage.Id, capabilityVersions, installedArtifacts, cancellationToken);
        await InstallOptimizationModelsAsync(loaded, modelPackage.Id, capabilityVersions, policyVersions, installedArtifacts, cancellationToken);
        var connectorVersions = await InstallConnectorsAsync(loaded, installedArtifacts, cancellationToken);
        var toolVersions = await InstallToolsAsync(loaded, modelPackage.Id, capabilityVersions, connectorVersions, installedArtifacts, cancellationToken);
        await InstallSkillsAsync(loaded, toolVersions, installedArtifacts, cancellationToken);
        await InstallAgentTemplatesAsync(context, loaded, modelPackage.Id, capabilityVersions, toolVersions, installedArtifacts, cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "reference-package.installed",
                AuditResult.Success,
                null,
                $"Reference package '{loaded.Manifest.PackageKey}' was installed.",
                SourceObjectType: "ModelPackageVersion",
                SourceObjectId: modelPackage.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new InstallReferencePackageResponse(
            loaded.Manifest.PackageKey,
            false,
            modelPackage,
            installedArtifacts,
            $"Reference package '{loaded.Manifest.PackageKey}' was installed and published.");
    }

    private async Task<ModelPackageVersionResponse> InstallOntologyStackAsync(
        LoadedReferencePackageManifest loaded,
        CancellationToken cancellationToken)
    {
        var manifest = loaded.Manifest;
        var publish = new PublishOntologyVersionRequest("Published by reference package installer.");

        var ontology = await ontologyService.CreateOntologyVersionAsync(
            new CreateOntologyVersionRequest(
                manifest.Ontology.Key,
                manifest.Ontology.VersionLabel,
                manifest.Ontology.Summary,
                loaded.ObjectTypes.Select(item => new CreateObjectTypeDefinitionRequest(
                    item.Key,
                    item.DisplayName,
                    item.Description,
                    item.VersionIdentityFieldsJson,
                    item.SafeSummary)).ToList(),
                loaded.RelationshipTypes.Select(item => new CreateSemanticRelationshipDefinitionRequest(
                    item.RelationshipType,
                    item.FromObjectType,
                    item.ToObjectType,
                    item.Description,
                    item.IsVersionRelationship)).ToList(),
                loaded.BomRelationships.Select(item => new CreateBomRelationshipDefinitionRequest(
                    item.RelationshipType,
                    item.ParentObjectType,
                    item.ChildObjectType,
                    item.QuantityAttributeKey,
                    item.UnitAttributeKey,
                    item.FindNumberAttributeKey,
                    item.ReferenceDesignatorAttributeKey,
                    item.LifecycleConstraintJson,
                    item.RequiresApproval,
                    item.AuditReferenceAttributeKey)).ToList()),
            cancellationToken);
        await ontologyService.PublishOntologyVersionAsync(ontology.Id, publish, cancellationToken);

        var semanticLayer = await ontologyService.CreateSemanticLayerVersionAsync(
            new CreateSemanticLayerVersionRequest(
                manifest.SemanticLayer.Key,
                manifest.SemanticLayer.VersionLabel,
                manifest.SemanticLayer.Summary,
                ontology.Id,
                loaded.SemanticLayerMappings.GraphNodeTypeMappingsJson,
                loaded.SemanticLayerMappings.GraphRelationshipTypeMappingsJson),
            cancellationToken);
        await ontologyService.PublishSemanticLayerVersionAsync(semanticLayer.Id, publish, cancellationToken);

        var lifecycle = await ontologyService.CreateLifecycleVocabularyVersionAsync(
            new CreateLifecycleVocabularyVersionRequest(
                manifest.Lifecycle.Key,
                manifest.Lifecycle.VersionLabel,
                manifest.Lifecycle.Summary,
                loaded.Lifecycle.States.Select(item => new CreateLifecycleStateDefinitionRequest(
                    item.Key,
                    item.DisplayName,
                    item.Category,
                    item.SortOrder,
                    item.IsTerminal)).ToList(),
                loaded.Lifecycle.Transitions.Select(item => new CreateLifecycleTransitionDefinitionRequest(
                    item.FromStateKey,
                    item.ToStateKey,
                    item.RequiresApproval,
                    item.SafeSummary)).ToList()),
            cancellationToken);
        await ontologyService.PublishLifecycleVocabularyVersionAsync(lifecycle.Id, publish, cancellationToken);

        var attributeSchema = await ontologyService.CreateAttributeSchemaVersionAsync(
            new CreateAttributeSchemaVersionRequest(
                manifest.AttributeSchema.Key,
                manifest.AttributeSchema.VersionLabel,
                manifest.AttributeSchema.Summary,
                ontology.Id,
                loaded.Attributes.Select(item => new CreateAttributeDefinitionRequest(
                    item.AttributeKey,
                    item.AppliesToObjectType,
                    Enum.Parse<AttributeValueType>(item.ValueType, ignoreCase: true),
                    item.IsRequired,
                    item.ValidationRulesJson,
                    Enum.Parse<AttributeVisibility>(item.Visibility, ignoreCase: true),
                    item.RequiredPermissionKey,
                    item.IsSearchable,
                    item.IsAiFacing,
                    item.ClassificationKey,
                    item.DisplayName,
                    item.SafeSummary)).ToList()),
            cancellationToken);
        await ontologyService.PublishAttributeSchemaVersionAsync(attributeSchema.Id, publish, cancellationToken);

        var importProfileJson = JsonSerializer.Serialize(loaded.ImportProfile, JsonOptions);
        var queryIntentExtensionsJson = JsonSerializer.Serialize(loaded.QueryIntentExtensions, JsonOptions);

        var modelPackage = await ontologyService.CreateModelPackageVersionAsync(
            new CreateModelPackageVersionRequest(
                manifest.PackageKey,
                manifest.Name,
                manifest.VersionLabel,
                manifest.Summary,
                ontology.Id,
                semanticLayer.Id,
                lifecycle.Id,
                attributeSchema.Id,
                importProfileJson,
                queryIntentExtensionsJson),
            cancellationToken);

        return await ontologyService.PublishModelPackageVersionAsync(modelPackage.Id, publish, cancellationToken);
    }

    private async Task<Dictionary<string, Guid>> InstallCapabilitiesAsync(
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        var capabilityVersions = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in loaded.Capabilities)
        {
            var created = await capabilityDefinitionService.CreateAsync(
                new CreateCapabilityDefinitionRequest(
                    capability.Name,
                    capability.Description,
                    capability.CapabilityKey,
                    capability.OutcomeCategory,
                    capability.OutcomeSummary,
                    capability.OutcomeMetadata,
                    [modelPackageVersionId],
                    null,
                    capability.SuggestedQueryIntentRefs,
                    capability.FutureExtensionPlaceholders),
                cancellationToken);
            await capabilityDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await capabilityDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            capabilityVersions[capability.CapabilityKey] = created.VersionId;
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "capability",
                capability.CapabilityKey,
                created.ArtifactId,
                created.VersionId));
        }

        return capabilityVersions;
    }

    private async Task<Dictionary<string, Guid>> InstallBusinessPoliciesAsync(
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> capabilityVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        var policyVersions = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var policy in loaded.BusinessPolicies)
        {
            var referencedCapabilityIds = policy.ReferencedCapabilityKeys
                .Select(key => capabilityVersions[key])
                .ToList();

            var created = await businessPolicyDefinitionService.CreateAsync(
                new CreateBusinessPolicyDefinitionRequest(
                    policy.Name,
                    policy.Description,
                    policy.PolicyKey,
                    policy.ConstraintCategory,
                    policy.ConstraintSummary,
                    policy.ConstraintRules,
                    referencedCapabilityIds,
                    [modelPackageVersionId],
                    null,
                    policy.FutureExtensionPlaceholders),
                cancellationToken);
            await businessPolicyDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await businessPolicyDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            policyVersions[policy.PolicyKey] = created.VersionId;
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "business-policy",
                policy.PolicyKey,
                created.ArtifactId,
                created.VersionId));
        }

        return policyVersions;
    }

    private async Task InstallOptimizationModelsAsync(
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> capabilityVersions,
        IReadOnlyDictionary<string, Guid> policyVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");

        foreach (var optimization in loaded.OptimizationModels)
        {
            var created = await optimizationModelDefinitionService.CreateAsync(
                new CreateOptimizationModelDefinitionRequest(
                    optimization.Name,
                    optimization.Description,
                    optimization.OptimizationKey,
                    optimization.ObjectiveCategory,
                    optimization.ObjectiveSummary,
                    optimization.ObjectiveMetadata,
                    optimization.SolverConfiguration,
                    optimization.InputRequirements,
                    optimization.ReferencedCapabilityKeys.Select(key => capabilityVersions[key]).ToList(),
                    optimization.ReferencedBusinessPolicyKeys.Select(key => policyVersions[key]).ToList(),
                    [modelPackageVersionId],
                    null,
                    optimization.FutureExtensionPlaceholders),
                cancellationToken);
            await optimizationModelDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await optimizationModelDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "optimization-model",
                optimization.OptimizationKey,
                created.ArtifactId,
                created.VersionId));
        }
    }

    private async Task<Dictionary<string, Guid>> InstallConnectorsAsync(
        LoadedReferencePackageManifest loaded,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        var connectorVersions = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (loaded.Connectors.Count == 0)
        {
            return connectorVersions;
        }

        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        foreach (var connector in loaded.Connectors)
        {
            var created = await connectorDefinitionService.CreateAsync(
                new CreateConnectorDefinitionRequest(
                    connector.Name,
                    connector.Description,
                    connector.ConnectorKey,
                    connector.ConnectorKind,
                    connector.CallsExternalSystem,
                    connector.WritesExternalSystem,
                    connector.ExecutionEnabled,
                    connector.DisabledReason,
                    connector.CredentialScopeKey,
                    connector.SecretReferenceKey,
                    connector.SupportedOperations,
                    connector.CompositionMetadata,
                    connector.FutureExtensionPlaceholders),
                cancellationToken);
            await connectorDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await connectorDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            connectorVersions[connector.ConnectorKey] = created.VersionId;
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "connector",
                connector.ConnectorKey,
                created.ArtifactId,
                created.VersionId));
        }

        return connectorVersions;
    }

    private async Task<Dictionary<string, Guid>> InstallToolsAsync(
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> capabilityVersions,
        IReadOnlyDictionary<string, Guid> connectorVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        var toolVersions = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (loaded.Tools.Count == 0)
        {
            return toolVersions;
        }

        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        foreach (var tool in loaded.Tools)
        {
            Guid? connectorVersionId = null;
            if (!string.IsNullOrWhiteSpace(tool.ReferencedConnectorKey))
            {
                connectorVersionId = connectorVersions[tool.ReferencedConnectorKey];
            }

            var created = await toolDefinitionService.CreateAsync(
                new CreateToolDefinitionRequest(
                    tool.Name,
                    tool.Description,
                    tool.ToolKey,
                    tool.ToolCategory,
                    tool.RiskLevel,
                    tool.ReadOnly,
                    tool.CreatesPlatformArtifact,
                    tool.CreatesReviewTask,
                    tool.CreatesDecision,
                    tool.CallsExternalSystem,
                    tool.WritesExternalSystem,
                    tool.RequiresApproval,
                    tool.SupportsDryRun,
                    tool.RequiredPermissionKeys,
                    tool.InputSchemaJson,
                    tool.OutputSchemaJson,
                    tool.InternalHandlerKey,
                    null,
                    connectorVersionId,
                    [modelPackageVersionId],
                    null,
                    tool.ReferencedCapabilityKeys.Select(key => capabilityVersions[key]).ToList(),
                    null,
                    tool.AllowedQueryIntentKeys,
                    tool.CompositionMetadata,
                    tool.FutureExtensionPlaceholders),
                cancellationToken);
            await toolDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await toolDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            toolVersions[tool.ToolKey] = created.VersionId;
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "tool",
                tool.ToolKey,
                created.ArtifactId,
                created.VersionId));
        }

        return toolVersions;
    }

    private async Task InstallSkillsAsync(
        LoadedReferencePackageManifest loaded,
        IReadOnlyDictionary<string, Guid> toolVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        if (loaded.Skills.Count == 0)
        {
            return;
        }

        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        foreach (var skill in loaded.Skills)
        {
            var created = await skillDefinitionService.CreateAsync(
                new CreateSkillDefinitionRequest(
                    skill.Name,
                    skill.Description,
                    skill.SkillKey,
                    skill.SkillSummary,
                    skill.IsGloballyShared,
                    skill.InputSchemaJson,
                    skill.OutputSchemaJson,
                    skill.ReferencedToolKeys.Select(key => toolVersions[key]).ToList(),
                    skill.CompositionMetadata,
                    skill.FutureExtensionPlaceholders),
                cancellationToken);
            await skillDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await skillDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "skill",
                skill.SkillKey,
                created.ArtifactId,
                created.VersionId));
        }
    }

    private async Task InstallAgentTemplatesAsync(
        ActiveTenantContext context,
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> capabilityVersions,
        IReadOnlyDictionary<string, Guid> toolVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        if (loaded.AgentTemplates.Count == 0)
        {
            return;
        }

        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        var chatArtifacts = await governedChatArtifactSeeder.EnsurePlatformArtifactsAsync(context, cancellationToken);
        var queryIntentId = await EnsureQueryIntentAsync(context, cancellationToken);
        var retrievalStrategyId = await EnsureRetrievalStrategyAsync(context, cancellationToken);
        var optimizationVersions = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == OptimizationModelDefinitionArtifactTypes.OptimizationModel.ToUpperInvariant())
            .Join(
                dbContext.ArtifactVersions.Where(version => version.TenantId == context.TenantId && version.ReadinessState == ArtifactReadinessState.Published),
                artifact => artifact.Id,
                version => version.ArtifactId,
                (_, version) => version)
            .ToListAsync(cancellationToken);

        foreach (var template in loaded.AgentTemplates)
        {
            Guid? optimizationVersionId = null;
            if (template.ReferencedOptimizationModelKeys?.Count > 0)
            {
                var optimizationKey = template.ReferencedOptimizationModelKeys.First();
                optimizationVersionId = optimizationVersions
                    .Select(version => new { version.Id, Key = ExtractPayloadKey(version.PayloadJson, "optimizationKey") })
                    .FirstOrDefault(item => string.Equals(item.Key, optimizationKey, StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            }

            var created = await agentTemplateDefinitionService.CreateAsync(
                new CreateAgentTemplateDefinitionRequest(
                    template.Name,
                    template.Description,
                    template.TemplateKey,
                    template.PatternCategory,
                    template.PatternSummary,
                    template.PreferredRuntimeAdapterKey ?? AgentRuntimeAdapterKeys.PydanticAi,
                    [modelPackageVersionId],
                    null,
                    template.ReferencedCapabilityKeys.Select(key => capabilityVersions[key]).ToList(),
                    null,
                    optimizationVersionId is null ? null : [optimizationVersionId.Value],
                    chatArtifacts.PromptTemplate.VersionId,
                    chatArtifacts.ChatAnswerSchema.VersionId,
                    queryIntentId,
                    retrievalStrategyId,
                    template.ReferencedToolKeys?.Select(key => toolVersions[key]).ToList(),
                    template.CompositionMetadata,
                    template.FutureExtensionPlaceholders),
                cancellationToken);
            await agentTemplateDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await agentTemplateDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "agent-template",
                template.TemplateKey,
                created.ArtifactId,
                created.VersionId));
        }
    }

    private async Task<Guid> EnsureQueryIntentAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        var intent = await dbContext.QueryIntentVersions
            .SingleOrDefaultAsync(item => item.TenantId == context.TenantId && item.IntentKey == "object-360-context", cancellationToken);
        if (intent is not null)
        {
            return intent.Id;
        }

        intent = new QueryIntentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            IntentKey = "object-360-context",
            NormalizedIntentKey = "OBJECT-360-CONTEXT",
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            Name = "Object 360 context",
            Summary = "Reference package query intent.",
            IntentKind = QueryIntentKind.Object360Context,
            Source = QueryIntentSource.PlatformFixed,
            IsEnabled = true,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.QueryIntentVersions.Add(intent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return intent.Id;
    }

    private async Task<Guid> EnsureRetrievalStrategyAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        var strategy = await dbContext.RetrievalStrategyVersions
            .SingleOrDefaultAsync(item => item.TenantId == context.TenantId && item.StrategyKey == "trusted-graph-first", cancellationToken);
        if (strategy is not null)
        {
            return strategy.Id;
        }

        strategy = new RetrievalStrategyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            StrategyKey = "trusted-graph-first",
            NormalizedStrategyKey = "TRUSTED-GRAPH-FIRST",
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            Name = "Trusted graph first",
            Summary = "Reference package retrieval strategy.",
            GraphSpace = GraphSpace.Trusted,
            RequiredTrustState = TrustState.Trusted,
            RelationshipTypesJson = "[]",
            AllowsSemanticFallback = false,
            AllowsVectorFallback = false,
            Source = QueryIntentSource.PlatformFixed,
            IsEnabled = true,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RetrievalStrategyVersions.Add(strategy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return strategy.Id;
    }

    private async Task<IReadOnlyCollection<InstalledReferenceArtifactResponse>> LoadInstalledArtifactSummariesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<InstalledReferenceArtifactResponse>();

        var capabilities = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.NormalizedArtifactType == CapabilityDefinitionArtifactTypes.CapabilityDefinition.ToUpperInvariant())
            .Join(dbContext.ArtifactVersions.Where(version => version.TenantId == tenantId && version.ReadinessState == ArtifactReadinessState.Published),
                artifact => artifact.Id,
                version => version.ArtifactId,
                (artifact, version) => new { artifact, version })
            .ToListAsync(cancellationToken);
        artifacts.AddRange(capabilities.Select(item => new InstalledReferenceArtifactResponse(
            "capability",
            ExtractPayloadKey(item.version.PayloadJson, "capabilityKey") ?? item.artifact.Name,
            item.artifact.Id,
            item.version.Id)));

        return artifacts;
    }

    private static string? ExtractPayloadKey(string? payloadJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task RequireWildcardPermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(InstallAction, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(
            context.TenantId,
            context.UserId,
            IdentityPermissions.Wildcard,
            cancellationToken);
        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                InstallAction,
                "permission_denied",
                "Reference package installation requires tenant administrator wildcard permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("Reference package installation requires tenant administrator permission.");
        }
    }
}
