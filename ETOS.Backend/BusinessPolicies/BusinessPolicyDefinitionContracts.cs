namespace ETOS.Backend.BusinessPolicies;

public static class BusinessPolicyDefinitionPermissions
{
    public const string Read = "business-policies.read";
    public const string Create = "business-policies.create";
    public const string Readiness = "business-policies.readiness";
    public const string Admin = "business-policies.admin";
}

public static class BusinessPolicyDefinitionArtifactTypes
{
    public const string BusinessPolicyDefinition = "BusinessPolicyDefinitionVersion";
}

/// <summary>
/// Compile-time guard: business constraint policies must stay distinct from classification governance policies.
/// </summary>
public static class ClassificationPolicySeparationGuards
{
    public const string ClassificationPolicyEntityName = "PolicyVersion";
    public const string ClassificationApiRoutePrefix = "/api/admin/classification";
}

public sealed record BusinessPolicyCapabilityReferenceResponse(
    Guid CapabilityDefinitionVersionId,
    Guid CapabilityArtifactId,
    string CapabilityArtifactName,
    string CapabilityKey,
    string VersionLabel,
    string ReadinessState);

public sealed record BusinessPolicyModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record BusinessPolicyOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record BusinessPolicyDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? PolicyKey,
    string? ConstraintCategory,
    DateTimeOffset UpdatedAt);

public sealed record BusinessPolicyDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string PolicyKey,
    string ConstraintCategory,
    string ConstraintSummary,
    IReadOnlyDictionary<string, string> ConstraintRules,
    IReadOnlyCollection<BusinessPolicyCapabilityReferenceResponse> ReferencedCapabilities,
    IReadOnlyCollection<BusinessPolicyModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<BusinessPolicyOntologyReferenceResponse> CompatibleOntologies,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record BusinessPolicyDependencySummaryResponse(
    IReadOnlyCollection<BusinessPolicyCapabilityReferenceResponse> Capabilities,
    IReadOnlyCollection<BusinessPolicyModelPackageReferenceResponse> ModelPackages,
    IReadOnlyCollection<BusinessPolicyOntologyReferenceResponse> Ontologies);

public sealed record CreateBusinessPolicyDefinitionRequest(
    string Name,
    string? Description,
    string PolicyKey,
    string ConstraintCategory,
    string ConstraintSummary,
    IReadOnlyDictionary<string, string>? ConstraintRules,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateBusinessPolicyDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string PolicyKey,
    string ConstraintCategory,
    string ConstraintSummary,
    IReadOnlyDictionary<string, string>? ConstraintRules,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateBusinessPolicyDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateBusinessPolicyDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkBusinessPolicyDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishBusinessPolicyDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
