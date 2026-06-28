using System.Text.Json.Serialization;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Recommendations;

namespace ETOS.Backend.ReviewTasks;

public static class ReviewTaskPermissions
{
    public const string Read = "review_tasks.read";
    public const string Create = "review_tasks.create";
    public const string Assign = "review_tasks.assign";
    public const string Manage = "review_tasks.manage";
    public const string Admin = "review_tasks.admin";
}

public static class ReviewTaskTemplatePermissions
{
    public const string Read = "review_task_templates.read";
    public const string Create = "review_task_templates.create";
    public const string Readiness = "review_task_templates.readiness";
    public const string Admin = "review_task_templates.admin";
}

public static class ReviewTaskArtifactTypes
{
    public const string ReviewTask = "ReviewTaskVersion";
}

public static class ReviewTaskTemplateArtifactTypes
{
    public const string ReviewTaskTemplate = "ReviewTaskTemplateVersion";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskStatus
{
    Draft = 0,
    Open = 1,
    Blocked = 2,
    InReview = 3,
    Completed = 4,
    Cancelled = 5,
    NeedsReevaluation = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskSourceType
{
    Manual = 0,
    Recommendation = 1,
    DataQuality = 2,
    SecurityEvent = 3,
    AccessRequest = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskParticipantRole
{
    PrimaryOwner = 0,
    Reviewer = 1,
    Approver = 2,
    Observer = 3,
    Contributor = 4,
    EscalationContact = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskChainReason
{
    DataQualityPrerequisite = 0,
    GovernancePrerequisite = 1,
    ManualDependency = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskBlockingCondition
{
    PrerequisiteCompleted = 0,
    PrerequisiteAccepted = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTaskCompletionResolution
{
    Accepted = 0,
    Rejected = 1
}

public sealed record ReviewTaskSourceReferenceResponse(
    ReviewTaskSourceType SourceType,
    string SourceReference);

public sealed record ReviewTaskParticipantResponse(
    Guid UserId,
    ReviewTaskParticipantRole Role);

public sealed record ReviewTaskEvidenceReferenceResponse(
    Guid LinkId,
    EvidenceLinkType EvidenceType,
    Guid SourceId,
    string SafeSummary,
    TrustState TrustState);

public sealed record ReviewTaskEscalationPlaceholderResponse(
    bool Enabled,
    string? EscalationTargetRoleKey,
    string? EscalationPolicyId,
    string? SlaPolicyVersion);

public sealed record ReviewTaskChainLinkResponse(
    Guid Id,
    Guid BlockedTaskArtifactId,
    Guid BlockingTaskArtifactId,
    ReviewTaskChainReason ChainReason,
    ReviewTaskBlockingCondition BlockingCondition,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record ReviewTaskCommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record ReviewTaskPayloadResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Title,
    ReviewTaskSourceReferenceResponse Source,
    string ReviewTaskType,
    ReviewTaskStatus Status,
    Guid? PrimaryOwnerUserId,
    string? AssignedRoleKey,
    IReadOnlyCollection<ReviewTaskParticipantResponse> Participants,
    ReviewTaskPriority Priority,
    RecommendationRiskState Severity,
    TrustState TrustState,
    RecommendationConflictState ConflictState,
    decimal? ConfidenceScore,
    IReadOnlyCollection<ReviewTaskEvidenceReferenceResponse> EvidenceReferences,
    Guid? ReviewTemplateVersionId,
    Guid? RecommendationArtifactId,
    Guid? RecommendationVersionId,
    Guid? SuggestedActionId,
    Guid? DataQualityIssueId,
    Guid? SecurityEventId,
    Guid? AccessRequestId,
    Guid? AiTraceId,
    Guid? ContextPackageId,
    DateTimeOffset? DueDate,
    ReviewTaskEscalationPlaceholderResponse? EscalationPlaceholder,
    IReadOnlyCollection<Guid> PrerequisiteTaskIds,
    string? BlockingReason,
    IReadOnlyCollection<ReviewTaskChainLinkResponse> ChainLinks,
    IReadOnlyCollection<ReviewTaskCommentResponse> Comments,
    string ArtifactReadinessState);

public sealed record ReviewTaskArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    ReviewTaskStatus? Status,
    ReviewTaskPriority? Priority,
    Guid? PrimaryOwnerUserId,
    ReviewTaskSourceType? SourceType,
    bool IsBlocked,
    DateTimeOffset UpdatedAt);

public sealed record CreateReviewTaskRequest(
    string Title,
    string ReviewTaskType,
    ReviewTaskSourceType SourceType,
    string? SourceReference,
    Guid? PrimaryOwnerUserId,
    string? AssignedRoleKey,
    IReadOnlyCollection<CreateReviewTaskParticipantRequest>? Participants,
    RecommendationRiskState? Severity,
    TrustState? TrustState,
    RecommendationConflictState? ConflictState,
    IReadOnlyCollection<CreateReviewTaskEvidenceReferenceRequest>? EvidenceReferences,
    Guid? ReviewTemplateVersionId,
    DateTimeOffset? DueDate);

public sealed record CreateReviewTaskParticipantRequest(
    Guid UserId,
    ReviewTaskParticipantRole Role);

public sealed record CreateReviewTaskEvidenceReferenceRequest(
    EvidenceLinkType EvidenceType,
    Guid SourceId,
    string SafeSummary,
    TrustState? TrustState);

public sealed record CreateReviewTaskResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    ReviewTaskStatus Status);

public sealed record AssignReviewTaskRequest(
    Guid? PrimaryOwnerUserId,
    string? AssignedRoleKey,
    IReadOnlyCollection<CreateReviewTaskParticipantRequest>? Participants);

public sealed record AssignReviewTaskResponse(
    Guid ArtifactId,
    Guid VersionId,
    Guid? PrimaryOwnerUserId,
    string? AssignedRoleKey,
    IReadOnlyCollection<ReviewTaskParticipantResponse> Participants);

public sealed record UpdateReviewTaskStatusRequest(
    ReviewTaskStatus Status,
    string? BlockingReason);

public sealed record UpdateReviewTaskStatusResponse(
    Guid ArtifactId,
    Guid VersionId,
    ReviewTaskStatus Status,
    string? BlockingReason);

public sealed record AddReviewTaskCommentRequest(
    string Body);

public sealed record AddReviewTaskCommentResponse(
    Guid CommentId,
    Guid ArtifactId,
    Guid VersionId,
    ReviewTaskCommentResponse Comment);

public sealed record CompleteReviewTaskRequest(
    ReviewTaskCompletionResolution Resolution,
    string? Summary);

public sealed record CompleteReviewTaskResponse(
    Guid ArtifactId,
    Guid VersionId,
    ReviewTaskStatus Status,
    bool DecisionCreationDeferred,
    IReadOnlyCollection<Guid> UnblockedTaskArtifactIds);

public sealed record CreateEscalationReviewTaskResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    ReviewTaskStatus Status);
