using ETOS.Backend.AgentRuntime;

namespace ETOS.Backend.Agents;

public static class AgentDefinitionArtifactTypes
{
    public const string AgentVersion = "AgentVersion";
}

public sealed record AgentFallbackModelResponse(
    string ProviderKey,
    string ModelId,
    string TriggerReason);

public sealed record AgentCapabilityReferenceResponse(
    Guid CapabilityDefinitionVersionId,
    Guid CapabilityArtifactId,
    string CapabilityArtifactName,
    string CapabilityKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentBusinessPolicyReferenceResponse(
    Guid BusinessPolicyDefinitionVersionId,
    Guid BusinessPolicyArtifactId,
    string BusinessPolicyArtifactName,
    string PolicyKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentOptimizationModelReferenceResponse(
    Guid OptimizationModelVersionId,
    Guid OptimizationModelArtifactId,
    string OptimizationModelArtifactName,
    string OptimizationKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record AgentOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record AgentArtifactVersionReferenceResponse(
    Guid VersionId,
    Guid ArtifactId,
    string ArtifactType,
    string ArtifactName,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentQueryIntentReferenceResponse(
    Guid QueryIntentVersionId,
    string IntentKey,
    string VersionLabel,
    bool IsEnabled);

public sealed record AgentRetrievalStrategyReferenceResponse(
    Guid RetrievalStrategyVersionId,
    string StrategyKey,
    string VersionLabel,
    bool IsEnabled);

public sealed record AgentToolReferenceResponse(
    Guid ToolDefinitionVersionId,
    Guid ToolArtifactId,
    string ToolArtifactName,
    string VersionLabel,
    string ReadinessState,
    string RiskLevel);

public sealed record AgentSkillReferenceResponse(
    Guid SkillDefinitionVersionId,
    Guid SkillArtifactId,
    string SkillArtifactName,
    string SkillKey,
    string VersionLabel,
    string ReadinessState);

public sealed record AgentTypeReferenceResponse(
    Guid AgentTypeDefinitionVersionId,
    Guid AgentTypeArtifactId,
    string AgentTypeArtifactName,
    string TypeKey,
    string VersionLabel,
    string ReadinessState,
    string RiskBaseline);

public sealed record AgentDerivedCapabilityRiskResponse(
    string EffectiveRiskLevel,
    IReadOnlyCollection<AgentToolRiskContributionResponse> ToolRiskContributions,
    AgentRetrievalRiskResponse RetrievalRisk,
    string PermissionCeiling);

public sealed record AgentToolRiskContributionResponse(
    Guid ToolDefinitionVersionId,
    string RiskLevel);

public sealed record AgentRetrievalRiskResponse(
    bool AllowsSemanticFallback,
    bool AllowsVectorFallback);

public sealed record AgentDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? AgentKey,
    string? DisplayName,
    string? PreferredRuntimeAdapterKey,
    DateTimeOffset UpdatedAt);

public sealed record AgentDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string AgentKey,
    string DisplayName,
    string? AgentDescription,
    AgentTypeReferenceResponse? AgentType,
    Guid? SourceAgentTemplateVersionId,
    string PreferredRuntimeAdapterKey,
    IReadOnlyCollection<AgentModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<AgentOntologyReferenceResponse> CompatibleOntologies,
    IReadOnlyCollection<AgentCapabilityReferenceResponse> ReferencedCapabilities,
    IReadOnlyCollection<AgentBusinessPolicyReferenceResponse> ReferencedBusinessPolicies,
    IReadOnlyCollection<AgentOptimizationModelReferenceResponse> ReferencedOptimizationModels,
    AgentArtifactVersionReferenceResponse? PromptTemplate,
    AgentArtifactVersionReferenceResponse? OutputSchema,
    AgentQueryIntentReferenceResponse? QueryIntent,
    AgentRetrievalStrategyReferenceResponse? RetrievalStrategy,
    IReadOnlyCollection<AgentToolReferenceResponse> ReferencedTools,
    IReadOnlyCollection<AgentSkillReferenceResponse> ReferencedSkills,
    string PrimaryModelProviderKey,
    string PrimaryModelId,
    IReadOnlyCollection<AgentFallbackModelResponse> FallbackModels,
    bool SafeModeEnabled,
    bool PreviewModeDefault,
    string? BlockedModeMessage,
    IReadOnlyCollection<string> CompatibilityTestNotes,
    IReadOnlyCollection<string> CompatibilityFixtureKeys,
    AgentDerivedCapabilityRiskResponse? DerivedCapabilityRisk,
    Guid CreatedByUserId,
    IReadOnlyDictionary<string, string> CompositionMetadata);

public sealed record AgentDependencySummaryResponse(
    AgentTypeReferenceResponse? AgentType,
    IReadOnlyCollection<AgentCapabilityReferenceResponse> Capabilities,
    IReadOnlyCollection<AgentBusinessPolicyReferenceResponse> BusinessPolicies,
    IReadOnlyCollection<AgentOptimizationModelReferenceResponse> OptimizationModels,
    IReadOnlyCollection<AgentModelPackageReferenceResponse> ModelPackages,
    IReadOnlyCollection<AgentOntologyReferenceResponse> Ontologies,
    AgentArtifactVersionReferenceResponse? PromptTemplate,
    AgentArtifactVersionReferenceResponse? OutputSchema,
    AgentQueryIntentReferenceResponse? QueryIntent,
    AgentRetrievalStrategyReferenceResponse? RetrievalStrategy,
    IReadOnlyCollection<AgentToolReferenceResponse> Tools,
    IReadOnlyCollection<AgentSkillReferenceResponse> Skills);

public sealed record CreateAgentDefinitionRequest(
    string Name,
    string? Description,
    string AgentKey,
    string DisplayName,
    string? AgentDescription,
    Guid AgentTypeDefinitionVersionId,
    Guid? SourceAgentTemplateVersionId,
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
    IReadOnlyCollection<Guid>? ReferencedSkillDefinitionVersionIds,
    string PrimaryModelProviderKey,
    string PrimaryModelId,
    IReadOnlyCollection<AgentFallbackModelRequest>? FallbackModels,
    bool SafeModeEnabled,
    bool PreviewModeDefault,
    string? BlockedModeMessage,
    IReadOnlyCollection<string>? CompatibilityTestNotes,
    IReadOnlyCollection<string>? CompatibilityFixtureKeys,
    IReadOnlyDictionary<string, string>? CompositionMetadata);

public sealed record AgentFallbackModelRequest(
    string ProviderKey,
    string ModelId,
    string TriggerReason);

public sealed record CreateAgentDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string AgentKey,
    string DisplayName,
    string? AgentDescription,
    Guid AgentTypeDefinitionVersionId,
    Guid? SourceAgentTemplateVersionId,
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
    IReadOnlyCollection<Guid>? ReferencedSkillDefinitionVersionIds,
    string PrimaryModelProviderKey,
    string PrimaryModelId,
    IReadOnlyCollection<AgentFallbackModelRequest>? FallbackModels,
    bool SafeModeEnabled,
    bool PreviewModeDefault,
    string? BlockedModeMessage,
    IReadOnlyCollection<string>? CompatibilityTestNotes,
    IReadOnlyCollection<string>? CompatibilityFixtureKeys,
    IReadOnlyDictionary<string, string>? CompositionMetadata);

public sealed record CreateAgentDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateAgentDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateAgentFromTemplateRequest(
    Guid SourceAgentTemplateVersionId,
    string? AgentKey,
    string? DisplayName,
    string? Description,
    Guid? AgentTypeDefinitionVersionId,
    string PrimaryModelProviderKey,
    string PrimaryModelId);

public sealed record CreateAgentFromPromptRequest(
    string Prompt,
    Guid? AgentTypeDefinitionVersionId,
    string PrimaryModelProviderKey,
    string PrimaryModelId);

public sealed record MarkAgentDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes,
    AgentDerivedCapabilityRiskResponse? DerivedCapabilityRisk);

public sealed record PublishAgentDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);

public sealed record UpdateAgentModelConfigRequest(
    string PrimaryModelProviderKey,
    string PrimaryModelId,
    IReadOnlyCollection<AgentFallbackModelRequest>? FallbackModels);

public sealed record UpdateAgentModelConfigResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string ReadinessState,
    bool CreatedNewVersion);

public static class AgentMvpBlockedRuntimeAdapters
{
    public static readonly IReadOnlyCollection<string> All =
    [
        AgentRuntimeAdapterKeys.Hermes,
        AgentRuntimeAdapterKeys.LangGraph
    ];
}
