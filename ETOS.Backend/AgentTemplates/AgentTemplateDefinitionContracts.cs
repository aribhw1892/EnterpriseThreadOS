using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Agents;

namespace ETOS.Backend.AgentTemplates;

public static class AgentTemplateDefinitionPermissions
{
    public const string Read = "agent-templates.read";
    public const string Create = "agent-templates.create";
    public const string Readiness = "agent-templates.readiness";
    public const string Admin = "agent-templates.admin";
}

public static class AgentTemplateDefinitionArtifactTypes
{
    public const string AgentTemplate = "AgentTemplateVersion";
}

/// <summary>
/// Compile-time guard: reusable agent pattern templates must stay distinct from tenant AgentVersion runtime records.
/// </summary>
public static class FutureAgentArtifactTypes
{
    [Obsolete("Use AgentDefinitionArtifactTypes.AgentVersion from ETOS.Backend.Agents.")]
    public const string AgentVersion = AgentDefinitionArtifactTypes.AgentVersion;
    public const string AgentCapabilityProfile = "AgentCapabilityProfileVersion";
}

public sealed record AgentTemplateCapabilityReferenceResponse(
    Guid CapabilityDefinitionVersionId,
    Guid CapabilityArtifactId,
    string CapabilityArtifactName,
    string CapabilityKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentTemplateBusinessPolicyReferenceResponse(
    Guid BusinessPolicyDefinitionVersionId,
    Guid BusinessPolicyArtifactId,
    string BusinessPolicyArtifactName,
    string PolicyKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentTemplateOptimizationModelReferenceResponse(
    Guid OptimizationModelVersionId,
    Guid OptimizationModelArtifactId,
    string OptimizationModelArtifactName,
    string OptimizationKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentTemplateModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record AgentTemplateOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record AgentTemplateArtifactVersionReferenceResponse(
    Guid VersionId,
    Guid ArtifactId,
    string ArtifactType,
    string ArtifactName,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentTemplateQueryIntentReferenceResponse(
    Guid QueryIntentVersionId,
    string IntentKey,
    string VersionLabel,
    bool IsEnabled);

public sealed record AgentTemplateRetrievalStrategyReferenceResponse(
    Guid RetrievalStrategyVersionId,
    string StrategyKey,
    string VersionLabel,
    bool IsEnabled);

public sealed record AgentTemplateToolReferenceResponse(
    Guid ToolDefinitionVersionId,
    Guid ToolArtifactId,
    string ToolArtifactName,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentTemplateDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? TemplateKey,
    string? PatternCategory,
    DateTimeOffset UpdatedAt);

public sealed record AgentTemplateDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string TemplateKey,
    string PatternCategory,
    string PatternSummary,
    string PreferredRuntimeAdapterKey,
    IReadOnlyCollection<AgentTemplateModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<AgentTemplateOntologyReferenceResponse> CompatibleOntologies,
    IReadOnlyCollection<AgentTemplateCapabilityReferenceResponse> ReferencedCapabilities,
    IReadOnlyCollection<AgentTemplateBusinessPolicyReferenceResponse> ReferencedBusinessPolicies,
    IReadOnlyCollection<AgentTemplateOptimizationModelReferenceResponse> ReferencedOptimizationModels,
    AgentTemplateArtifactVersionReferenceResponse? PromptTemplate,
    AgentTemplateArtifactVersionReferenceResponse? OutputSchema,
    AgentTemplateQueryIntentReferenceResponse? QueryIntent,
    AgentTemplateRetrievalStrategyReferenceResponse? RetrievalStrategy,
    IReadOnlyCollection<AgentTemplateToolReferenceResponse> ReferencedTools,
    IReadOnlyDictionary<string, string> CompositionMetadata,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record AgentTemplateDependencySummaryResponse(
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

public sealed record CreateAgentTemplateDefinitionRequest(
    string Name,
    string? Description,
    string TemplateKey,
    string PatternCategory,
    string PatternSummary,
    string? PreferredRuntimeAdapterKey,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedOptimizationModelVersionIds,
    Guid? PromptTemplateVersionId,
    Guid? OutputSchemaVersionId,
    Guid? QueryIntentVersionId,
    Guid? RetrievalStrategyVersionId,
    IReadOnlyCollection<Guid>? ReferencedToolDefinitionVersionIds,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateAgentTemplateDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string TemplateKey,
    string PatternCategory,
    string PatternSummary,
    string? PreferredRuntimeAdapterKey,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedOptimizationModelVersionIds,
    Guid? PromptTemplateVersionId,
    Guid? OutputSchemaVersionId,
    Guid? QueryIntentVersionId,
    Guid? RetrievalStrategyVersionId,
    IReadOnlyCollection<Guid>? ReferencedToolDefinitionVersionIds,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateAgentTemplateDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateAgentTemplateDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkAgentTemplateDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishAgentTemplateDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
