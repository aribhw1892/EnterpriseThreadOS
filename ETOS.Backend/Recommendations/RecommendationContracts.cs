using System.Text.Json.Serialization;
using ETOS.Backend.GraphMemory;

namespace ETOS.Backend.Recommendations;

public static class RecommendationPermissions
{
    public const string Read = "recommendations.read";
    public const string Create = "recommendations.create";
    public const string Review = "recommendations.review";
    public const string Readiness = "recommendations.readiness";
    public const string Admin = "recommendations.admin";
}

public static class RecommendationArtifactTypes
{
    public const string Recommendation = "RecommendationVersion";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationType
{
    DataQuality = 0,
    BomSync = 1,
    ReworkRisk = 2,
    IdentityResolution = 3,
    DocumentLink = 4,
    Security = 5,
    Policy = 6,
    ArtifactUpgrade = 7,
    LifecycleConflict = 8,
    ImportValidation = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationCreationSource
{
    Manual = 0,
    DataQuality = 1,
    BomComparison = 2,
    Chat = 3,
    Dashboard = 4,
    Report = 5,
    AgentDeferred = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationLifecycleStatus
{
    Draft = 0,
    Reviewed = 1,
    Accepted = 2,
    Rejected = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationRiskState
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationCapabilityState
{
    ReadOnlyAnalysis = 0,
    ReviewRequired = 1,
    ActionDeferred = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationConflictState
{
    None = 0,
    Partial = 1,
    Blocked = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceLinkType
{
    GraphNode = 0,
    GraphRelationship = 1,
    Document = 2,
    DataQualityIssue = 3,
    GraphDiff = 4,
    GraphSnapshot = 5,
    BomComparisonRun = 6,
    ImportBatch = 7,
    AiTrace = 8,
    RetrievalRun = 9,
    ContextPackage = 10,
    ManualNote = 11,
    Dashboard = 12,
    Report = 13,
    AuditRecord = 14
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuggestedActionStatus
{
    Proposed = 0,
    SelectedForReview = 1,
    Rejected = 2,
    Deferred = 3,
    Superseded = 4,
    ConvertedToReviewTask = 5
}

public sealed record RecommendationSourceReferenceResponse(
    string Kind,
    Guid Id);

public sealed record RecommendationEvidenceLinkResponse(
    Guid LinkId,
    EvidenceLinkType EvidenceType,
    Guid SourceId,
    string SafeSummary,
    TrustState TrustState,
    bool PermissionFiltered);

public sealed record RecommendationSuggestedActionResponse(
    Guid ActionId,
    string Title,
    string Kind,
    RecommendationRiskState RiskScore,
    string? RequiredReviewPath,
    SuggestedActionStatus Status,
    string? Description);

public sealed record RecommendationRelatedObjectResponse(
    Guid? GraphNodeId,
    string? ObjectType);

public sealed record RecommendationExplainabilityResponse(
    Guid? AiTraceId,
    Guid? ContextPackageId,
    Guid? RetrievalRunId);

public sealed record RecommendationPayloadResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Title,
    string Summary,
    RecommendationType RecommendationType,
    RecommendationCreationSource CreationSource,
    RecommendationSourceReferenceResponse? SourceReference,
    RecommendationRiskState RiskState,
    RecommendationCapabilityState CapabilityState,
    TrustState TrustState,
    RecommendationConflictState ConflictState,
    RecommendationLifecycleStatus LifecycleStatus,
    IReadOnlyCollection<RecommendationEvidenceLinkResponse> EvidenceLinks,
    IReadOnlyCollection<RecommendationSuggestedActionResponse> SuggestedActions,
    IReadOnlyCollection<RecommendationRelatedObjectResponse> RelatedObjects,
    RecommendationExplainabilityResponse Explainability,
    bool OutcomeTrackingRequired,
    string? UniqueSourceKey,
    string ArtifactReadinessState);

public sealed record RecommendationArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    RecommendationLifecycleStatus? LifecycleStatus,
    RecommendationType? RecommendationType,
    DateTimeOffset UpdatedAt);

public sealed record CreateRecommendationRequest(
    string Title,
    string Summary,
    RecommendationType RecommendationType,
    RecommendationCreationSource CreationSource,
    RecommendationRiskState RiskState,
    RecommendationCapabilityState CapabilityState,
    IReadOnlyCollection<CreateRecommendationEvidenceLinkRequest> EvidenceLinks,
    IReadOnlyCollection<CreateRecommendationSuggestedActionRequest> SuggestedActions,
    IReadOnlyCollection<CreateRecommendationRelatedObjectRequest>? RelatedObjects,
    RecommendationExplainabilityResponse? Explainability,
    bool OutcomeTrackingRequired);

public sealed record CreateRecommendationEvidenceLinkRequest(
    EvidenceLinkType EvidenceType,
    Guid SourceId,
    string SafeSummary,
    TrustState? TrustState,
    bool PermissionFiltered);

public sealed record CreateRecommendationSuggestedActionRequest(
    string Title,
    string Kind,
    RecommendationRiskState RiskScore,
    string? RequiredReviewPath,
    string? Description);

public sealed record CreateRecommendationRelatedObjectRequest(
    Guid? GraphNodeId,
    string? ObjectType);

public sealed record CreateRecommendationResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    RecommendationLifecycleStatus LifecycleStatus);

public sealed record MarkRecommendationReviewedResponse(
    Guid ArtifactId,
    Guid VersionId,
    RecommendationLifecycleStatus LifecycleStatus,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record MarkRecommendationReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    TrustState TrustState,
    RecommendationConflictState ConflictState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record UpdateSuggestedActionStatusRequest(
    SuggestedActionStatus Status);

public sealed record UpdateSuggestedActionStatusResponse(
    Guid ArtifactId,
    Guid VersionId,
    Guid ActionId,
    SuggestedActionStatus Status);
