using System.Text.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Agents;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Workflows;
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
    IImportMappingArtifactSeeder importMappingArtifactSeeder,
    IAgentDefinitionService agentDefinitionService,
    IWorkflowDefinitionService workflowDefinitionService,
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
            await EnsureInstalledReferencePackageContinuityAsync(context, loaded, existing, cancellationToken);
            var artifacts = await LoadInstalledArtifactSummariesAsync(context.TenantId, cancellationToken);
            return new InstallReferencePackageResponse(
                loaded.Manifest.PackageKey,
                true,
                existing,
                artifacts,
                $"Reference package '{loaded.Manifest.PackageKey}' is already published for this tenant. Ensured missing reference artifacts and mapping assistant agent.");
        }

        var modelPackage = await InstallOntologyStackAsync(loaded, cancellationToken);
        var installedArtifacts = new List<InstalledReferenceArtifactResponse>();
        var capabilityVersions = await InstallCapabilitiesAsync(loaded, modelPackage.Id, installedArtifacts, cancellationToken);
        var policyVersions = await InstallBusinessPoliciesAsync(loaded, modelPackage.Id, capabilityVersions, installedArtifacts, cancellationToken);
        var optimizationVersions = await InstallOptimizationModelsAsync(loaded, modelPackage.Id, capabilityVersions, policyVersions, installedArtifacts, cancellationToken);
        var connectorVersions = await InstallConnectorsAsync(loaded, installedArtifacts, cancellationToken);
        var toolVersions = await InstallToolsAsync(loaded, modelPackage.Id, capabilityVersions, connectorVersions, installedArtifacts, cancellationToken);
        await InstallSkillsAsync(loaded, toolVersions, installedArtifacts, cancellationToken);
        await InstallAgentTemplatesAsync(context, loaded, modelPackage.Id, capabilityVersions, toolVersions, installedArtifacts, cancellationToken);
        await EnsureAnalysisAgentTypeAsync(context, cancellationToken);
        await InstallWorkflowsAsync(context, loaded, modelPackage.Id, toolVersions, policyVersions, optimizationVersions, installedArtifacts, cancellationToken);
        await EnsureMappingAssistantAgentAsync(context, loaded, cancellationToken);

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

    private async Task<Dictionary<string, Guid>> InstallOptimizationModelsAsync(
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> capabilityVersions,
        IReadOnlyDictionary<string, Guid> policyVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");
        var optimizationVersions = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

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
            optimizationVersions[optimization.OptimizationKey] = created.VersionId;
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "optimization-model",
                optimization.OptimizationKey,
                created.ArtifactId,
                created.VersionId));
        }

        return optimizationVersions;
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
        var mappingArtifacts = await importMappingArtifactSeeder.EnsurePlatformArtifactsAsync(context, cancellationToken);
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
            if (await HasPublishedAgentTemplateAsync(context.TenantId, template.TemplateKey, cancellationToken))
            {
                continue;
            }

            await InstallAgentTemplateAsync(
                context,
                loaded,
                template,
                modelPackageVersionId,
                capabilityVersions,
                toolVersions,
                publish,
                chatArtifacts,
                mappingArtifacts,
                queryIntentId,
                retrievalStrategyId,
                optimizationVersions,
                installedArtifacts,
                cancellationToken);
        }
    }

    private async Task EnsureInstalledReferencePackageContinuityAsync(
        ActiveTenantContext context,
        LoadedReferencePackageManifest loaded,
        ModelPackageVersionResponse existingPackage,
        CancellationToken cancellationToken)
    {
        await EnsureAnalysisAgentTypeAsync(context, cancellationToken);

        var installedArtifacts = new List<InstalledReferenceArtifactResponse>();
        var capabilityVersions = await ResolvePublishedCapabilityVersionsAsync(context.TenantId, cancellationToken);
        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");

        foreach (var capability in loaded.Capabilities)
        {
            if (capabilityVersions.ContainsKey(capability.CapabilityKey))
            {
                continue;
            }

            var created = await capabilityDefinitionService.CreateAsync(
                new CreateCapabilityDefinitionRequest(
                    capability.Name,
                    capability.Description,
                    capability.CapabilityKey,
                    capability.OutcomeCategory,
                    capability.OutcomeSummary,
                    capability.OutcomeMetadata,
                    [existingPackage.Id],
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

        var connectorVersions = await ResolvePublishedConnectorVersionsAsync(context.TenantId, cancellationToken);
        foreach (var connector in loaded.Connectors)
        {
            if (connectorVersions.ContainsKey(connector.ConnectorKey))
            {
                continue;
            }

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
        }

        var toolVersions = await ResolvePublishedToolVersionsAsync(context.TenantId, cancellationToken);
        foreach (var tool in loaded.Tools)
        {
            if (toolVersions.ContainsKey(tool.ToolKey))
            {
                continue;
            }

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
                    [existingPackage.Id],
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
        }

        await InstallAgentTemplatesAsync(
            context,
            loaded,
            existingPackage.Id,
            capabilityVersions,
            toolVersions,
            installedArtifacts,
            cancellationToken);

        var policyVersions = await ResolvePublishedBusinessPolicyVersionsAsync(context.TenantId, cancellationToken);
        var optimizationVersions = await ResolvePublishedOptimizationModelVersionsAsync(context.TenantId, cancellationToken);
        await InstallWorkflowsAsync(
            context,
            loaded,
            existingPackage.Id,
            toolVersions,
            policyVersions,
            optimizationVersions,
            installedArtifacts,
            cancellationToken);
        await EnsureMappingAssistantAgentAsync(context, loaded, cancellationToken);
    }

    private async Task InstallAgentTemplateAsync(
        ActiveTenantContext context,
        LoadedReferencePackageManifest loaded,
        ReferenceAgentTemplateDocument template,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> capabilityVersions,
        IReadOnlyDictionary<string, Guid> toolVersions,
        PublishArtifactVersionRequest publish,
        GovernedChatPlatformArtifacts chatArtifacts,
        ImportMappingPlatformArtifacts mappingArtifacts,
        Guid queryIntentId,
        Guid retrievalStrategyId,
        IReadOnlyCollection<ArtifactVersion> optimizationVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
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

        var isMappingAssistant = string.Equals(
            template.PatternCategory,
            "mapping-assistant",
            StringComparison.OrdinalIgnoreCase);

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
                isMappingAssistant ? mappingArtifacts.PromptTemplate.VersionId : chatArtifacts.PromptTemplate.VersionId,
                isMappingAssistant ? mappingArtifacts.OutputSchema.VersionId : chatArtifacts.ChatAnswerSchema.VersionId,
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

    private async Task EnsureAnalysisAgentTypeAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        if (await TryResolveDefaultAgentTypeVersionIdAsync(context.TenantId, cancellationToken) is not null)
        {
            return;
        }

        var payload = AgentTypeDefinitionPayloadParser.Create(
            "analysis-agent",
            "Governed analysis and investigation agents for local development.",
            ["object-360-context", "bom-impact-context"],
            "investigator",
            ToolRiskLevels.Medium);

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = AgentTypeDefinitionArtifactTypes.AgentTypeDefinition,
            NormalizedArtifactType = AgentTypeDefinitionArtifactTypes.AgentTypeDefinition.ToUpperInvariant(),
            Name = "Analysis Agent Type",
            Description = "Reference package agent type catalog entry.",
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
            Summary = "Reference package analysis agent type.",
            PayloadJson = AgentTypeDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CompatibilityStatus = ArtifactCompatibilityStatus.Compatible,
            CompatibilitySummary = "Reference package publish.",
            PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedByUserId = context.UserId,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishSummary = "Reference package publish."
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasPublishedAgentTemplateAsync(
        Guid tenantId,
        string templateKey,
        CancellationToken cancellationToken)
    {
        var templateVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in templateVersions)
        {
            var templatePayload = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(templatePayload.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<Dictionary<string, Guid>> ResolvePublishedCapabilityVersionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await ResolvePublishedArtifactVersionsAsync(
            tenantId,
            CapabilityDefinitionArtifactTypes.CapabilityDefinition,
            "capabilityKey",
            cancellationToken);

    private async Task<Dictionary<string, Guid>> ResolvePublishedConnectorVersionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await ResolvePublishedArtifactVersionsAsync(
            tenantId,
            ConnectorDefinitionArtifactTypes.ConnectorDefinition,
            "connectorKey",
            cancellationToken);

    private async Task<Dictionary<string, Guid>> ResolvePublishedToolVersionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await ResolvePublishedArtifactVersionsAsync(
            tenantId,
            ToolDefinitionArtifactTypes.ToolDefinition,
            "toolKey",
            cancellationToken);

    private async Task<Dictionary<string, Guid>> ResolvePublishedArtifactVersionsAsync(
        Guid tenantId,
        string artifactType,
        string payloadKeyProperty,
        CancellationToken cancellationToken)
    {
        var normalizedType = artifactType.ToUpperInvariant();
        var versions = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.NormalizedArtifactType == normalizedType)
            .Join(
                dbContext.ArtifactVersions.Where(version =>
                    version.TenantId == tenantId && version.ReadinessState == ArtifactReadinessState.Published),
                artifact => artifact.Id,
                version => version.ArtifactId,
                (artifact, version) => version)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        {
            var key = ExtractPayloadKey(version.PayloadJson, payloadKeyProperty);
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = version.Id;
            }
        }

        return result;
    }

    private async Task InstallWorkflowsAsync(
        ActiveTenantContext context,
        LoadedReferencePackageManifest loaded,
        Guid modelPackageVersionId,
        IReadOnlyDictionary<string, Guid> toolVersions,
        IReadOnlyDictionary<string, Guid> policyVersions,
        IReadOnlyDictionary<string, Guid> optimizationVersions,
        List<InstalledReferenceArtifactResponse> installedArtifacts,
        CancellationToken cancellationToken)
    {
        if (loaded.Workflows.Count == 0)
        {
            return;
        }

        await ReviewTaskDevelopmentTemplateSeeder.SeedPublishedTemplatesAsync(
            dbContext,
            context.TenantId,
            context.UserId,
            null,
            cancellationToken);

        var publish = new PublishArtifactVersionRequest("Published by reference package installer.");

        foreach (var workflow in loaded.Workflows)
        {
            if (await HasPublishedWorkflowAsync(context.TenantId, workflow.WorkflowKey, cancellationToken))
            {
                continue;
            }

            var agentVersionIds = new List<Guid>();
            var toolVersionIds = new List<Guid>();
            var policyVersionIds = new List<Guid>();
            var optimizationVersionIds = new List<Guid>();
            var stepRequests = new List<WorkflowStepDefinitionRequest>();

            foreach (var step in workflow.Steps)
            {
                Guid? agentVersionId = null;
                Guid? toolVersionId = null;
                Guid? policyVersionId = null;
                Guid? optimizationVersionId = null;
                Guid? reviewTaskTemplateVersionId = null;

                if (!string.IsNullOrWhiteSpace(step.ToolKey))
                {
                    toolVersionId = toolVersions[step.ToolKey];
                    toolVersionIds.Add(toolVersionId.Value);
                }

                if (!string.IsNullOrWhiteSpace(step.AgentTemplateKey))
                {
                    agentVersionId = await EnsureWorkflowAgentVersionAsync(
                        context,
                        step.AgentTemplateKey,
                        workflow.WorkflowKey,
                        cancellationToken);
                    agentVersionIds.Add(agentVersionId.Value);
                }

                if (!string.IsNullOrWhiteSpace(step.PolicyKey))
                {
                    policyVersionId = policyVersions[step.PolicyKey];
                    policyVersionIds.Add(policyVersionId.Value);
                }

                if (!string.IsNullOrWhiteSpace(step.OptimizationModelKey))
                {
                    optimizationVersionId = optimizationVersions[step.OptimizationModelKey];
                    optimizationVersionIds.Add(optimizationVersionId.Value);
                }

                if (!string.IsNullOrWhiteSpace(step.ReviewTaskTemplateKey))
                {
                    reviewTaskTemplateVersionId = await ResolvePublishedReviewTaskTemplateVersionIdAsync(
                        context.TenantId,
                        step.ReviewTaskTemplateKey,
                        cancellationToken);
                }

                stepRequests.Add(new WorkflowStepDefinitionRequest(
                    step.StepKey,
                    step.StepType,
                    string.IsNullOrWhiteSpace(step.SafeModeOnBlock) ? WorkflowStepSafeModeBehaviors.Skip : step.SafeModeOnBlock,
                    step.DependsOnStepKeys,
                    agentVersionId,
                    toolVersionId,
                    policyVersionId,
                    optimizationVersionId,
                    step.SourceStepKey,
                    reviewTaskTemplateVersionId));
            }

            var created = await workflowDefinitionService.CreateAsync(
                new CreateWorkflowDefinitionRequest(
                    workflow.Name,
                    workflow.Description,
                    workflow.WorkflowKey,
                    workflow.Name,
                    workflow.Description,
                    workflow.WorkflowScope,
                    stepRequests,
                    null,
                    null,
                    agentVersionIds.Distinct().ToList(),
                    toolVersionIds.Distinct().ToList(),
                    policyVersionIds.Distinct().ToList(),
                    optimizationVersionIds.Distinct().ToList(),
                    [modelPackageVersionId],
                    null,
                    workflow.SafeModeEnabled,
                    workflow.PreviewModeDefault,
                    null,
                    workflow.AllowPartialCompletion,
                    string.IsNullOrWhiteSpace(workflow.DefaultStepSafeModeBehavior)
                        ? WorkflowStepSafeModeBehaviors.Skip
                        : workflow.DefaultStepSafeModeBehavior,
                    new WorkflowTriggerConfigRequest(true, false, null, false, null),
                    null,
                    null,
                    null),
                cancellationToken);

            await workflowDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
            await workflowDefinitionService.PublishAsync(created.ArtifactId, created.VersionId, publish, cancellationToken);
            installedArtifacts.Add(new InstalledReferenceArtifactResponse(
                "workflow",
                workflow.WorkflowKey,
                created.ArtifactId,
                created.VersionId));
        }
    }

    private async Task<Guid> EnsureWorkflowAgentVersionAsync(
        ActiveTenantContext context,
        string agentTemplateKey,
        string workflowKey,
        CancellationToken cancellationToken)
    {
        var agentKey = $"{workflowKey}-agent";
        var existingAgent = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == context.TenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentDefinitionArtifactTypes.AgentVersion.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in existingAgent)
        {
            var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(payload.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase))
            {
                return version.Id;
            }
        }

        var templateVersionId = await ResolvePublishedAgentTemplateVersionIdAsync(
            context.TenantId,
            agentTemplateKey,
            cancellationToken);
        var agentTypeVersionId = await TryResolveDefaultAgentTypeVersionIdAsync(context.TenantId, cancellationToken)
            ?? throw new RequestValidationException($"Agent type definition is required before installing workflow agent '{agentKey}'.");

        var created = await agentDefinitionService.CreateFromTemplateAsync(
            new CreateAgentFromTemplateRequest(
                templateVersionId,
                agentKey,
                $"{workflowKey} Agent",
                $"Reference workflow agent for {workflowKey}.",
                agentTypeVersionId,
                "deterministic",
                "mock-v1"),
            cancellationToken);
        await agentDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
        await agentDefinitionService.PublishAsync(
            created.ArtifactId,
            created.VersionId,
            new PublishArtifactVersionRequest("Published by reference package installer."),
            cancellationToken);
        return created.VersionId;
    }

    private async Task<Guid> ResolvePublishedAgentTemplateVersionIdAsync(
        Guid tenantId,
        string templateKey,
        CancellationToken cancellationToken)
    {
        var templateVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in templateVersions)
        {
            var templatePayload = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(templatePayload.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
            {
                return version.Id;
            }
        }

        throw new RequestValidationException($"Published agent template '{templateKey}' was not found.");
    }

    private async Task<Guid> ResolvePublishedReviewTaskTemplateVersionIdAsync(
        Guid tenantId,
        string templateKey,
        CancellationToken cancellationToken)
    {
        var templateVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in templateVersions)
        {
            var templatePayload = ReviewTaskTemplatePayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(templatePayload.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
            {
                return version.Id;
            }
        }

        throw new RequestValidationException($"Published review task template '{templateKey}' was not found.");
    }

    private async Task<bool> HasPublishedWorkflowAsync(
        Guid tenantId,
        string workflowKey,
        CancellationToken cancellationToken)
    {
        var workflowVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == WorkflowDefinitionArtifactTypes.WorkflowVersion.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in workflowVersions)
        {
            var payload = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(payload.WorkflowKey, workflowKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<Dictionary<string, Guid>> ResolvePublishedBusinessPolicyVersionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await ResolvePublishedArtifactVersionsAsync(
            tenantId,
            BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition,
            "policyKey",
            cancellationToken);

    private async Task<Dictionary<string, Guid>> ResolvePublishedOptimizationModelVersionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await ResolvePublishedArtifactVersionsAsync(
            tenantId,
            OptimizationModelDefinitionArtifactTypes.OptimizationModel,
            "optimizationKey",
            cancellationToken);

    private async Task EnsureMappingAssistantAgentAsync(
        ActiveTenantContext context,
        LoadedReferencePackageManifest loaded,
        CancellationToken cancellationToken)
    {
        const string mappingAgentKey = "import-mapping-assistant";
        var mappingTemplate = loaded.AgentTemplates.FirstOrDefault(item =>
            string.Equals(item.TemplateKey, mappingAgentKey, StringComparison.OrdinalIgnoreCase));
        if (mappingTemplate is null)
        {
            return;
        }

        var existingAgent = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == context.TenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentDefinitionArtifactTypes.AgentVersion.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in existingAgent)
        {
            var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(payload.AgentKey, mappingAgentKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var templateVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == context.TenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        Guid? sourceTemplateVersionId = null;
        foreach (var version in templateVersion)
        {
            var templatePayload = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(templatePayload.TemplateKey, mappingAgentKey, StringComparison.OrdinalIgnoreCase))
            {
                sourceTemplateVersionId = version.Id;
                break;
            }
        }

        if (sourceTemplateVersionId is null)
        {
            return;
        }

        var agentTypeVersionId = await TryResolveDefaultAgentTypeVersionIdAsync(context.TenantId, cancellationToken);
        if (agentTypeVersionId is null)
        {
            return;
        }

        var providerKey = mappingTemplate.CompositionMetadata?.GetValueOrDefault("primaryModelProviderKey") ?? "openai";
        var modelId = mappingTemplate.CompositionMetadata?.GetValueOrDefault("primaryModelId") ?? "gpt-4o-mini";

        var created = await agentDefinitionService.CreateFromTemplateAsync(
            new CreateAgentFromTemplateRequest(
                sourceTemplateVersionId.Value,
                mappingAgentKey,
                mappingTemplate.Name,
                mappingTemplate.Description,
                agentTypeVersionId,
                providerKey,
                modelId),
            cancellationToken);
        await agentDefinitionService.MarkReadyAsync(created.ArtifactId, created.VersionId, cancellationToken);
        await agentDefinitionService.PublishAsync(
            created.ArtifactId,
            created.VersionId,
            new PublishArtifactVersionRequest("Published by reference package installer."),
            cancellationToken);
    }

    private async Task<Guid?> TryResolveDefaultAgentTypeVersionIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var version = await (
            from artifact in dbContext.Artifacts.AsNoTracking()
            join artifactVersion in dbContext.ArtifactVersions.AsNoTracking() on artifact.Id equals artifactVersion.ArtifactId
            where artifact.TenantId == tenantId
                && artifact.NormalizedArtifactType == AgentTypeDefinitionArtifactTypes.AgentTypeDefinition.ToUpperInvariant()
                && artifactVersion.ReadinessState == ArtifactReadinessState.Published
            orderby artifactVersion.PublishedAt descending, artifactVersion.CreatedAt descending
            select artifactVersion)
            .FirstOrDefaultAsync(cancellationToken);

        return version?.Id;
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
