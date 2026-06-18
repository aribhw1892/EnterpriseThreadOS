namespace ETOS.Backend.Capabilities;

public static class CapabilityDefinitionPermissions
{
    public const string Read = "capabilities.read";
    public const string Create = "capabilities.create";
    public const string Readiness = "capabilities.readiness";
    public const string Admin = "capabilities.admin";
}

public static class CapabilityDefinitionArtifactTypes
{
    public const string CapabilityDefinition = "CapabilityDefinitionVersion";
}

/// <summary>
/// Compile-time guard: business capability definitions must stay distinct from future agent runtime profiles.
/// </summary>
public static class FutureAgentCapabilityProfileArtifactTypes
{
    public const string AgentCapabilityProfile = "AgentCapabilityProfileVersion";
}

public sealed record CapabilityModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record CapabilityOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record CapabilityDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? CapabilityKey,
    string? OutcomeCategory,
    DateTimeOffset UpdatedAt);

public sealed record CapabilityDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string CapabilityKey,
    string OutcomeCategory,
    string OutcomeSummary,
    IReadOnlyDictionary<string, string> OutcomeMetadata,
    IReadOnlyCollection<CapabilityModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<CapabilityOntologyReferenceResponse> CompatibleOntologies,
    IReadOnlyCollection<string> SuggestedQueryIntentRefs,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record CapabilityDependencySummaryResponse(
    IReadOnlyCollection<CapabilityModelPackageReferenceResponse> ModelPackages,
    IReadOnlyCollection<CapabilityOntologyReferenceResponse> Ontologies);

public sealed record CreateCapabilityDefinitionRequest(
    string Name,
    string? Description,
    string CapabilityKey,
    string OutcomeCategory,
    string OutcomeSummary,
    IReadOnlyDictionary<string, string>? OutcomeMetadata,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<string>? SuggestedQueryIntentRefs,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateCapabilityDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string CapabilityKey,
    string OutcomeCategory,
    string OutcomeSummary,
    IReadOnlyDictionary<string, string>? OutcomeMetadata,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<string>? SuggestedQueryIntentRefs,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateCapabilityDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateCapabilityDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkCapabilityDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishCapabilityDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
