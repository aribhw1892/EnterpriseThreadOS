namespace ETOS.Backend.OptimizationModels;

public static class OptimizationModelDefinitionPermissions
{
    public const string Read = "optimization-models.read";
    public const string Create = "optimization-models.create";
    public const string Readiness = "optimization-models.readiness";
    public const string Admin = "optimization-models.admin";
}

public static class OptimizationModelDefinitionArtifactTypes
{
    public const string OptimizationModel = "OptimizationModelVersion";
}

/// <summary>
/// Compile-time guard: optimization objective metadata must stay distinct from agent templates and LLM runtime wiring.
/// </summary>
public static class OptimizationModelSeparationGuards
{
    public const string AgentTemplateArtifactType = "AgentTemplateVersion";
    public const string AgentVersionArtifactType = "AgentVersion";
}

public sealed record OptimizationModelCapabilityReferenceResponse(
    Guid CapabilityDefinitionVersionId,
    Guid CapabilityArtifactId,
    string CapabilityArtifactName,
    string CapabilityKey,
    string VersionLabel,
    string ReadinessState);

public sealed record OptimizationModelBusinessPolicyReferenceResponse(
    Guid BusinessPolicyDefinitionVersionId,
    Guid BusinessPolicyArtifactId,
    string BusinessPolicyArtifactName,
    string PolicyKey,
    string VersionLabel,
    string ReadinessState);

public sealed record OptimizationModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record OptimizationModelOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record OptimizationModelDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? OptimizationKey,
    string? ObjectiveCategory,
    DateTimeOffset UpdatedAt);

public sealed record OptimizationModelDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string OptimizationKey,
    string ObjectiveCategory,
    string ObjectiveSummary,
    IReadOnlyDictionary<string, string> ObjectiveMetadata,
    IReadOnlyDictionary<string, string> SolverConfiguration,
    IReadOnlyCollection<string> InputRequirements,
    IReadOnlyCollection<OptimizationModelCapabilityReferenceResponse> ReferencedCapabilities,
    IReadOnlyCollection<OptimizationModelBusinessPolicyReferenceResponse> ReferencedBusinessPolicies,
    IReadOnlyCollection<OptimizationModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<OptimizationModelOntologyReferenceResponse> CompatibleOntologies,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record OptimizationModelDependencySummaryResponse(
    IReadOnlyCollection<OptimizationModelCapabilityReferenceResponse> Capabilities,
    IReadOnlyCollection<OptimizationModelBusinessPolicyReferenceResponse> BusinessPolicies,
    IReadOnlyCollection<OptimizationModelPackageReferenceResponse> ModelPackages,
    IReadOnlyCollection<OptimizationModelOntologyReferenceResponse> Ontologies);

public sealed record CreateOptimizationModelDefinitionRequest(
    string Name,
    string? Description,
    string OptimizationKey,
    string ObjectiveCategory,
    string ObjectiveSummary,
    IReadOnlyDictionary<string, string>? ObjectiveMetadata,
    IReadOnlyDictionary<string, string>? SolverConfiguration,
    IReadOnlyCollection<string>? InputRequirements,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateOptimizationModelDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string OptimizationKey,
    string ObjectiveCategory,
    string ObjectiveSummary,
    IReadOnlyDictionary<string, string>? ObjectiveMetadata,
    IReadOnlyDictionary<string, string>? SolverConfiguration,
    IReadOnlyCollection<string>? InputRequirements,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateOptimizationModelDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateOptimizationModelDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkOptimizationModelDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishOptimizationModelDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
