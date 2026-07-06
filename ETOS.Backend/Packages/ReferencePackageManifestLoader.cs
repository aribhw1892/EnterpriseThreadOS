using System.Text.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Packages;

public interface IReferencePackageManifestLoader
{
    LoadedReferencePackageManifest Load(string packageKey);
    string ReadPackageFile(string packageKey, string relativePath);
}

public sealed record LoadedReferencePackageManifest(
    ReferencePackageManifestDocument Manifest,
    string PackageDirectory,
    IReadOnlyList<ReferenceObjectTypeDocument> ObjectTypes,
    IReadOnlyList<ReferenceRelationshipTypeDocument> RelationshipTypes,
    IReadOnlyList<ReferenceBomRelationshipDocument> BomRelationships,
    ReferenceSemanticLayerMappingsDocument SemanticLayerMappings,
    ReferenceLifecycleDocument Lifecycle,
    IReadOnlyList<ReferenceAttributeDocument> Attributes,
    ModelPackageImportProfile ImportProfile,
    ModelPackageQueryIntentExtensions QueryIntentExtensions,
    IReadOnlyList<ReferenceCapabilityDocument> Capabilities,
    IReadOnlyList<ReferenceBusinessPolicyDocument> BusinessPolicies,
    IReadOnlyList<ReferenceOptimizationModelDocument> OptimizationModels,
    IReadOnlyList<ReferenceAgentTemplateDocument> AgentTemplates,
    IReadOnlyList<ReferenceConnectorDocument> Connectors,
    IReadOnlyList<ReferenceToolDocument> Tools,
    IReadOnlyList<ReferenceSkillDocument> Skills,
    IReadOnlyList<ReferenceWorkflowDocument> Workflows);

public sealed class ReferencePackageManifestLoader(
    IWebHostEnvironment environment,
    IOptions<ReferencePackageOptions> options) : IReferencePackageManifestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public LoadedReferencePackageManifest Load(string packageKey)
    {
        var packageDirectory = ResolvePackageDirectory(packageKey);
        var manifestPath = Path.Combine(packageDirectory, "package.manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new RequestValidationException($"Reference package manifest was not found for key '{packageKey}'.");
        }

        var manifest = DeserializeFile<ReferencePackageManifestDocument>(manifestPath);
        if (!string.Equals(manifest.PackageKey, packageKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"Reference package key mismatch. Expected '{packageKey}', manifest declares '{manifest.PackageKey}'.");
        }

        return new LoadedReferencePackageManifest(
            manifest,
            packageDirectory,
            DeserializeRelative<ReferenceObjectTypeDocument[]>(packageDirectory, manifest.Ontology.ObjectTypesFile),
            DeserializeRelative<ReferenceRelationshipTypeDocument[]>(packageDirectory, manifest.Ontology.RelationshipTypesFile),
            DeserializeRelative<ReferenceBomRelationshipDocument[]>(packageDirectory, manifest.Ontology.BomRelationshipsFile),
            DeserializeRelative<ReferenceSemanticLayerMappingsDocument>(packageDirectory, "ontology/semantic-layer-mappings.json"),
            DeserializeRelative<ReferenceLifecycleDocument>(packageDirectory, "ontology/lifecycle.json"),
            DeserializeRelative<ReferenceAttributeDocument[]>(packageDirectory, "ontology/attribute-schema.json"),
            ModelPackageProfileParser.ParseImportProfile(ReadPackageFile(packageKey, manifest.Profiles.ImportProfileFile)),
            ModelPackageProfileParser.ParseQueryIntentExtensions(ReadPackageFile(packageKey, manifest.Profiles.QueryIntentExtensionsFile)),
            DeserializeRelative<ReferenceCapabilityDocument[]>(packageDirectory, manifest.Artifacts.CapabilitiesFile),
            DeserializeRelative<ReferenceBusinessPolicyDocument[]>(packageDirectory, manifest.Artifacts.BusinessPoliciesFile),
            DeserializeRelative<ReferenceOptimizationModelDocument[]>(packageDirectory, manifest.Artifacts.OptimizationModelsFile),
            DeserializeRelative<ReferenceAgentTemplateDocument[]>(packageDirectory, manifest.Artifacts.AgentTemplatesFile),
            DeserializeRelative<ReferenceConnectorDocument[]>(packageDirectory, manifest.Artifacts.ConnectorsFile),
            DeserializeRelative<ReferenceToolDocument[]>(packageDirectory, manifest.Artifacts.ToolsFile),
            DeserializeRelative<ReferenceSkillDocument[]>(packageDirectory, manifest.Artifacts.SkillsFile),
            DeserializeRelative<ReferenceWorkflowDocument[]>(packageDirectory, manifest.Artifacts.WorkflowsFile));
    }

    public string ReadPackageFile(string packageKey, string relativePath)
    {
        var packageDirectory = ResolvePackageDirectory(packageKey);
        var fullPath = Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new RequestValidationException($"Reference package file '{relativePath}' was not found for key '{packageKey}'.");
        }

        return File.ReadAllText(fullPath);
    }

    private string ResolvePackageDirectory(string packageKey)
    {
        var directoryName = packageKey switch
        {
            ManufacturingReferencePackageKeys.PackageKey => ManufacturingReferencePackageKeys.DirectoryName,
            _ => throw new RequestValidationException($"Unsupported reference package key '{packageKey}'.")
        };

        var packagesRoot = ResolvePackagesRoot();
        var packageDirectory = Path.Combine(packagesRoot, directoryName);
        if (!Directory.Exists(packageDirectory))
        {
            throw new RequestValidationException($"Reference package directory was not found at '{packageDirectory}'.");
        }

        return packageDirectory;
    }

    private string ResolvePackagesRoot()
    {
        var configured = options.Value.RootPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "..", "packages"),
            Path.Combine(environment.ContentRootPath, "..", "..", "packages"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new RequestValidationException("Reference packages root directory could not be resolved.");
    }

    private static T DeserializeRelative<T>(string packageDirectory, string relativePath)
    {
        var fullPath = Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return DeserializeFile<T>(fullPath);
    }

    private static T DeserializeFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new RequestValidationException($"Reference package file '{path}' could not be deserialized.");
    }
}

public sealed class ReferencePackageManifestDocument
{
    public required string PackageKey { get; init; }
    public required string Name { get; init; }
    public required string VersionLabel { get; init; }
    public string? Summary { get; init; }
    public required ReferenceOntologyManifestSection Ontology { get; init; }
    public required ReferenceNamedManifestSection SemanticLayer { get; init; }
    public required ReferenceNamedManifestSection Lifecycle { get; init; }
    public required ReferenceNamedManifestSection AttributeSchema { get; init; }
    public required ReferenceProfilesManifestSection Profiles { get; init; }
    public required ReferenceDemoImportsManifestSection DemoImports { get; init; }
    public required ReferenceArtifactsManifestSection Artifacts { get; init; }
}

public sealed class ReferenceOntologyManifestSection
{
    public required string Key { get; init; }
    public required string VersionLabel { get; init; }
    public string? Summary { get; init; }
    public required string ObjectTypesFile { get; init; }
    public required string RelationshipTypesFile { get; init; }
    public required string BomRelationshipsFile { get; init; }
}

public sealed class ReferenceNamedManifestSection
{
    public required string Key { get; init; }
    public required string VersionLabel { get; init; }
    public string? Summary { get; init; }
}

public sealed class ReferenceProfilesManifestSection
{
    public required string ImportProfileFile { get; init; }
    public required string QueryIntentExtensionsFile { get; init; }
}

public sealed class ReferenceDemoImportsManifestSection
{
    public required string FlatPartImportFile { get; init; }
    public required string BomComparisonFile { get; init; }
}

public sealed class ReferenceArtifactsManifestSection
{
    public required string CapabilitiesFile { get; init; }
    public required string BusinessPoliciesFile { get; init; }
    public required string OptimizationModelsFile { get; init; }
    public required string AgentTemplatesFile { get; init; }
    public required string ToolsFile { get; init; }
    public required string ConnectorsFile { get; init; }
    public required string SkillsFile { get; init; }
    public required string WorkflowsFile { get; init; }
}

public sealed class ReferenceObjectTypeDocument
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? VersionIdentityFieldsJson { get; init; }
    public required string SafeSummary { get; init; }
}

public sealed class ReferenceRelationshipTypeDocument
{
    public required string RelationshipType { get; init; }
    public required string FromObjectType { get; init; }
    public required string ToObjectType { get; init; }
    public string? Description { get; init; }
    public bool IsVersionRelationship { get; init; }
}

public sealed class ReferenceBomRelationshipDocument
{
    public required string RelationshipType { get; init; }
    public required string ParentObjectType { get; init; }
    public required string ChildObjectType { get; init; }
    public string? QuantityAttributeKey { get; init; }
    public string? UnitAttributeKey { get; init; }
    public string? FindNumberAttributeKey { get; init; }
    public string? ReferenceDesignatorAttributeKey { get; init; }
    public string? LifecycleConstraintJson { get; init; }
    public bool RequiresApproval { get; init; }
    public string? AuditReferenceAttributeKey { get; init; }
}

public sealed class ReferenceSemanticLayerMappingsDocument
{
    public required string GraphNodeTypeMappingsJson { get; init; }
    public required string GraphRelationshipTypeMappingsJson { get; init; }
}

public sealed class ReferenceLifecycleDocument
{
    public required IReadOnlyList<ReferenceLifecycleStateDocument> States { get; init; }
    public required IReadOnlyList<ReferenceLifecycleTransitionDocument> Transitions { get; init; }
}

public sealed class ReferenceLifecycleStateDocument
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Category { get; init; }
    public int SortOrder { get; init; }
    public bool IsTerminal { get; init; }
}

public sealed class ReferenceLifecycleTransitionDocument
{
    public required string FromStateKey { get; init; }
    public required string ToStateKey { get; init; }
    public bool RequiresApproval { get; init; }
    public string? SafeSummary { get; init; }
}

public sealed class ReferenceAttributeDocument
{
    public required string AttributeKey { get; init; }
    public required string AppliesToObjectType { get; init; }
    public required string ValueType { get; init; }
    public bool IsRequired { get; init; }
    public string? ValidationRulesJson { get; init; }
    public required string Visibility { get; init; }
    public string? RequiredPermissionKey { get; init; }
    public bool IsSearchable { get; init; }
    public bool IsAiFacing { get; init; }
    public string? ClassificationKey { get; init; }
    public string? DisplayName { get; init; }
    public required string SafeSummary { get; init; }
}

public sealed class ReferenceCapabilityDocument
{
    public required string CapabilityKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string OutcomeCategory { get; init; }
    public required string OutcomeSummary { get; init; }
    public IReadOnlyDictionary<string, string>? OutcomeMetadata { get; init; }
    public IReadOnlyCollection<string>? SuggestedQueryIntentRefs { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}

public sealed class ReferenceBusinessPolicyDocument
{
    public required string PolicyKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ConstraintCategory { get; init; }
    public required string ConstraintSummary { get; init; }
    public IReadOnlyDictionary<string, string>? ConstraintRules { get; init; }
    public required IReadOnlyCollection<string> ReferencedCapabilityKeys { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}

public sealed class ReferenceOptimizationModelDocument
{
    public required string OptimizationKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ObjectiveCategory { get; init; }
    public required string ObjectiveSummary { get; init; }
    public IReadOnlyDictionary<string, string>? ObjectiveMetadata { get; init; }
    public IReadOnlyDictionary<string, string>? SolverConfiguration { get; init; }
    public IReadOnlyCollection<string>? InputRequirements { get; init; }
    public required IReadOnlyCollection<string> ReferencedCapabilityKeys { get; init; }
    public required IReadOnlyCollection<string> ReferencedBusinessPolicyKeys { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}

public sealed class ReferenceAgentTemplateDocument
{
    public required string TemplateKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string PatternCategory { get; init; }
    public required string PatternSummary { get; init; }
    public string? PreferredRuntimeAdapterKey { get; init; }
    public required IReadOnlyCollection<string> ReferencedCapabilityKeys { get; init; }
    public IReadOnlyCollection<string>? ReferencedOptimizationModelKeys { get; init; }
    public required string QueryIntentKey { get; init; }
    public required string RetrievalStrategyKey { get; init; }
    public IReadOnlyCollection<string>? ReferencedToolKeys { get; init; }
    public IReadOnlyDictionary<string, string>? CompositionMetadata { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}

public sealed class ReferenceConnectorDocument
{
    public required string ConnectorKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ConnectorKind { get; init; }
    public bool CallsExternalSystem { get; init; }
    public bool WritesExternalSystem { get; init; }
    public bool ExecutionEnabled { get; init; }
    public string? DisabledReason { get; init; }
    public required string CredentialScopeKey { get; init; }
    public required string SecretReferenceKey { get; init; }
    public IReadOnlyCollection<string>? SupportedOperations { get; init; }
    public IReadOnlyDictionary<string, string>? CompositionMetadata { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}

public sealed class ReferenceToolDocument
{
    public required string ToolKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ToolCategory { get; init; }
    public required string RiskLevel { get; init; }
    public bool ReadOnly { get; init; } = true;
    public bool CreatesPlatformArtifact { get; init; }
    public bool CreatesReviewTask { get; init; }
    public bool CreatesDecision { get; init; }
    public bool CallsExternalSystem { get; init; }
    public bool WritesExternalSystem { get; init; }
    public bool RequiresApproval { get; init; }
    public bool SupportsDryRun { get; init; } = true;
    public IReadOnlyCollection<string>? RequiredPermissionKeys { get; init; }
    public required string InputSchemaJson { get; init; }
    public required string OutputSchemaJson { get; init; }
    public string? InternalHandlerKey { get; init; }
    public string? ReferencedConnectorKey { get; init; }
    public required IReadOnlyCollection<string> ReferencedCapabilityKeys { get; init; }
    public IReadOnlyCollection<string>? AllowedQueryIntentKeys { get; init; }
    public IReadOnlyDictionary<string, string>? CompositionMetadata { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}

public sealed class ReferenceWorkflowDocument
{
    public required string WorkflowKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string WorkflowScope { get; init; }
    public bool SafeModeEnabled { get; init; }
    public bool PreviewModeDefault { get; init; }
    public bool AllowPartialCompletion { get; init; } = true;
    public string? DefaultStepSafeModeBehavior { get; init; }
    public required IReadOnlyList<ReferenceWorkflowStepDocument> Steps { get; init; }
}

public sealed class ReferenceWorkflowStepDocument
{
    public required string StepKey { get; init; }
    public required string StepType { get; init; }
    public string? SafeModeOnBlock { get; init; }
    public IReadOnlyCollection<string>? DependsOnStepKeys { get; init; }
    public string? ToolKey { get; init; }
    public string? AgentTemplateKey { get; init; }
    public string? PolicyKey { get; init; }
    public string? OptimizationModelKey { get; init; }
    public string? SourceStepKey { get; init; }
    public string? ReviewTaskTemplateKey { get; init; }
}

public sealed class ReferenceSkillDocument
{
    public required string SkillKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string SkillSummary { get; init; }
    public bool IsGloballyShared { get; init; }
    public required string InputSchemaJson { get; init; }
    public required string OutputSchemaJson { get; init; }
    public required IReadOnlyCollection<string> ReferencedToolKeys { get; init; }
    public IReadOnlyDictionary<string, string>? CompositionMetadata { get; init; }
    public IReadOnlyCollection<string>? FutureExtensionPlaceholders { get; init; }
}
