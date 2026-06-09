using System.Text.Json.Serialization;
using ETOS.Backend.Identity;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Artifacts;

public sealed class Artifact : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string ArtifactType { get; set; }

    public required string NormalizedArtifactType { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public Guid OwnerUserId { get; set; }

    public EtosUser? OwnerUser { get; set; }

    public ArtifactLifecycleState LifecycleState { get; set; } = ArtifactLifecycleState.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ArtifactVersion> Versions { get; set; } = [];

    public List<ArtifactRelationship> SourceRelationships { get; set; } = [];

    public List<ArtifactRelationship> TargetRelationships { get; set; } = [];
}

public sealed class ArtifactVersion : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ArtifactId { get; set; }

    public Artifact? Artifact { get; set; }

    public required string VersionLabel { get; set; }

    public required string NormalizedVersionLabel { get; set; }

    public string? Summary { get; set; }

    public string? PayloadJson { get; set; }

    public ArtifactReadinessState ReadinessState { get; set; } = ArtifactReadinessState.Draft;

    public ArtifactCompatibilityStatus CompatibilityStatus { get; set; } = ArtifactCompatibilityStatus.Unknown;

    public string? CompatibilitySummary { get; set; }

    public ArtifactPolicyRiskStatus PolicyRiskStatus { get; set; } = ArtifactPolicyRiskStatus.NotEvaluated;

    public Guid CreatedByUserId { get; set; }

    public EtosUser? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? PublishedByUserId { get; set; }

    public EtosUser? PublishedByUser { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishSummary { get; set; }

    public List<ArtifactDependency> Dependencies { get; set; } = [];

    public List<ArtifactDependency> RequiredBy { get; set; } = [];
}

public sealed class ArtifactRelationship : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SourceArtifactId { get; set; }

    public Artifact? SourceArtifact { get; set; }

    public Guid TargetArtifactId { get; set; }

    public Artifact? TargetArtifact { get; set; }

    public ArtifactRelationshipType RelationshipType { get; set; } = ArtifactRelationshipType.RelatedTo;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ArtifactDependency : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid DependentVersionId { get; set; }

    public ArtifactVersion? DependentVersion { get; set; }

    public Guid RequiredArtifactId { get; set; }

    public Artifact? RequiredArtifact { get; set; }

    public Guid RequiredVersionId { get; set; }

    public ArtifactVersion? RequiredVersion { get; set; }

    public ArtifactDependencyKind DependencyKind { get; set; } = ArtifactDependencyKind.DependsOn;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactLifecycleState
{
    Active = 0,
    Archived = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactReadinessState
{
    Draft = 0,
    Blocked = 1,
    RequiresApproval = 2,
    Ready = 3,
    Published = 4,
    Rejected = 5,
    Retired = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactCompatibilityStatus
{
    Unknown = 0,
    Compatible = 1,
    Warning = 2,
    Incompatible = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactPolicyRiskStatus
{
    NotEvaluated = 0,
    Acceptable = 1,
    RequiresApproval = 2,
    Blocked = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactRelationshipType
{
    RelatedTo = 0,
    Uses = 1,
    References = 2,
    DerivedFrom = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactDependencyKind
{
    DependsOn = 0,
    OptionalDependsOn = 1
}
