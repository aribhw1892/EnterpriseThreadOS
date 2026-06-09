namespace ETOS.Backend.Artifacts;

public static class ArtifactPermissions
{
    public const string Read = "artifacts.read";
    public const string Create = "artifacts.create";
    public const string Publish = "artifacts.publish";
    public const string Admin = "artifacts.admin";
}

public sealed record CreateArtifactRequest(
    string ArtifactType,
    string Name,
    string? Description,
    Guid? OwnerUserId);

public sealed record CreateArtifactVersionRequest(
    string VersionLabel,
    string? Summary,
    string? PayloadJson,
    ArtifactReadinessState ReadinessState,
    ArtifactCompatibilityStatus CompatibilityStatus,
    string? CompatibilitySummary);

public sealed record CreateArtifactRelationshipRequest(
    Guid TargetArtifactId,
    ArtifactRelationshipType RelationshipType,
    string? Description);

public sealed record CreateArtifactDependencyRequest(
    Guid RequiredArtifactId,
    Guid RequiredVersionId,
    ArtifactDependencyKind DependencyKind);

public sealed record PublishArtifactVersionRequest(string? Summary);

public sealed record ArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    Guid OwnerUserId,
    ArtifactLifecycleState LifecycleState,
    ArtifactVersionSummaryResponse? LatestVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ArtifactDetailResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    Guid OwnerUserId,
    ArtifactLifecycleState LifecycleState,
    IReadOnlyCollection<ArtifactVersionSummaryResponse> Versions,
    IReadOnlyCollection<ArtifactRelationshipResponse> Relationships,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ArtifactVersionSummaryResponse(
    Guid Id,
    Guid TenantId,
    Guid ArtifactId,
    string VersionLabel,
    string? Summary,
    ArtifactReadinessState ReadinessState,
    ArtifactCompatibilityStatus CompatibilityStatus,
    string? CompatibilitySummary,
    ArtifactPolicyRiskStatus PolicyRiskStatus,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    string? PublishSummary);

public sealed record ArtifactRelationshipResponse(
    Guid Id,
    Guid TenantId,
    Guid SourceArtifactId,
    Guid TargetArtifactId,
    string TargetArtifactName,
    ArtifactRelationshipType RelationshipType,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record ArtifactDependencyResponse(
    Guid Id,
    Guid TenantId,
    Guid DependentVersionId,
    Guid RequiredArtifactId,
    string RequiredArtifactName,
    Guid RequiredVersionId,
    string RequiredVersionLabel,
    ArtifactReadinessState RequiredReadinessState,
    ArtifactDependencyKind DependencyKind,
    DateTimeOffset CreatedAt);

public sealed record ArtifactImpactResponse(
    IReadOnlyCollection<ArtifactDependencyResponse> Dependencies,
    IReadOnlyCollection<ArtifactDependentResponse> Dependents);

public sealed record ArtifactDependentResponse(
    Guid DependencyId,
    Guid TenantId,
    Guid DependentArtifactId,
    string DependentArtifactName,
    Guid DependentVersionId,
    string DependentVersionLabel,
    ArtifactDependencyKind DependencyKind,
    DateTimeOffset CreatedAt);

public sealed record ArtifactReadinessResponse(
    Guid ArtifactId,
    Guid VersionId,
    ArtifactReadinessState StoredReadinessState,
    ArtifactReadinessState RecalculatedReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    ArtifactCompatibilityStatus CompatibilityStatus,
    ArtifactPolicyRiskStatus PolicyRiskStatus);

public sealed record PublishArtifactVersionResult(
    bool Succeeded,
    ArtifactReadinessState ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    ArtifactCompatibilityStatus CompatibilityStatus,
    ArtifactPolicyRiskStatus PolicyRiskStatus,
    ArtifactVersionSummaryResponse Version);
