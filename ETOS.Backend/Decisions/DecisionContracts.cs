using System.Text.Json.Serialization;

namespace ETOS.Backend.Decisions;

public static class DecisionPermissions
{
    public const string Read = "decisions.read";
    public const string Vote = "decisions.vote";
    public const string Manage = "decisions.manage";
    public const string Admin = "decisions.admin";
}

public static class DecisionArtifactTypes
{
    public const string Decision = "DecisionArtifact";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecisionStatus
{
    PendingVotes = 0,
    Finalized = 1,
    BlockedConflict = 2,
    Escalated = 3,
    Superseded = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecisionConflictState
{
    None = 0,
    Blocked = 1,
    Resolved = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecisionVoteKind
{
    Approve = 0,
    Reject = 1,
    Abstain = 2,
    Dissent = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecisionApprovalRuleMode
{
    SingleApprover = 0,
    AllRequired = 1,
    AnyOne = 2,
    Majority = 3,
    RoleBased = 4
}

public sealed record DecisionApprovalRuleSnapshotResponse(
    DecisionApprovalRuleMode Mode,
    IReadOnlyCollection<string> RequiredRoles,
    Guid? OutcomeTaxonomyVersionId,
    bool OutcomeTrackingRequired);

public sealed record DecisionEvidenceReferenceResponse(
    Guid LinkId,
    string EvidenceType,
    Guid SourceId,
    string SafeSummary,
    string TrustState);

public sealed record DecisionVoteResponse(
    Guid Id,
    Guid UserId,
    DecisionVoteKind Vote,
    string? Comment,
    decimal? Confidence,
    DateTimeOffset CreatedAt);

public sealed record DecisionCommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record DecisionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string Title,
    DecisionStatus Status,
    string OutcomeKey,
    string OutcomeSummary,
    string? DecisionReason,
    DecisionConflictState ConflictState,
    Guid ReviewTaskArtifactId,
    Guid ReviewTaskVersionId,
    Guid? ReviewTemplateVersionId,
    Guid? RecommendationArtifactId,
    Guid? DataQualityIssueId,
    Guid? SecurityEventId,
    Guid? AccessRequestId,
    Guid? AiTraceId,
    Guid? ContextPackageId,
    Guid? ParentDecisionArtifactId,
    DecisionApprovalRuleSnapshotResponse ApprovalRuleSnapshot,
    IReadOnlyCollection<Guid> ParticipantUserIds,
    IReadOnlyCollection<DecisionEvidenceReferenceResponse> EvidenceReferences,
    bool OutcomeTrackingRequired,
    Guid? OutcomeTaxonomyVersionId,
    DateTimeOffset? FinalizedAt,
    Guid? FinalizedByUserId,
    IReadOnlyCollection<DecisionVoteResponse> Votes,
    IReadOnlyCollection<DecisionCommentResponse> Comments,
    string ContextViewRoute);

public sealed record DecisionSummaryResponse(
    Guid ArtifactId,
    string ArtifactType,
    string Title,
    string Status,
    string OutcomeKey,
    IReadOnlyCollection<string> ParticipantUserIds,
    int EvidenceCount,
    string ConflictState,
    string OutcomeSummary,
    string ContextViewRoute);

public sealed record CastDecisionVoteRequest(
    DecisionVoteKind Vote,
    string? Comment,
    decimal? Confidence);

public sealed record CastDecisionVoteResponse(
    Guid VoteId,
    Guid ArtifactId,
    Guid VersionId,
    DecisionStatus Status,
    DecisionConflictState ConflictState,
    DecisionVoteResponse Vote);

public sealed record AddDecisionCommentRequest(string Body);

public sealed record AddDecisionCommentResponse(
    Guid CommentId,
    Guid ArtifactId,
    Guid VersionId,
    DecisionCommentResponse Comment);

public sealed record FinalizeDecisionResponse(
    Guid ArtifactId,
    Guid VersionId,
    DecisionStatus Status,
    string OutcomeKey,
    string OutcomeSummary,
    DecisionConflictState ConflictState);

public sealed record CreateDecisionEscalationResponse(
    Guid ReviewTaskArtifactId,
    Guid ReviewTaskVersionId,
    Guid DecisionArtifactId,
    Guid DecisionVersionId);

public sealed record ReviewTaskCompletionResult(
    Guid DecisionArtifactId,
    Guid DecisionVersionId,
    DecisionStatus Status);
