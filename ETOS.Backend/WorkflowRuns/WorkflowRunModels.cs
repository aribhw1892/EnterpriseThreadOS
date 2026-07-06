using ETOS.Backend.Tenancy;

namespace ETOS.Backend.WorkflowRuns;

public static class WorkflowRunStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Blocked = "Blocked";
    public const string PreviewSucceeded = "PreviewSucceeded";
    public const string SafeModeCompleted = "SafeModeCompleted";
    public const string SafeModeBlocked = "SafeModeBlocked";
}

public sealed class WorkflowRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public required string Status { get; set; }
    public bool IsPreview { get; set; }
    public bool SafeModeApplied { get; set; }
    public bool PartialCompletion { get; set; }
    public required string InputSafeSummaryJson { get; set; }
    public string? OutputSafeSummaryJson { get; set; }
    public string? StepResultsJson { get; set; }
    public string? InheritedRiskSnapshotJson { get; set; }
    public string? RuntimeTrustRecalculationJson { get; set; }
    public string? RecommendationArtifactIdsJson { get; set; }
    public string? ReviewTaskArtifactIdsJson { get; set; }
    public Guid? AuditRecordId { get; set; }
    public Guid? AiTraceRecordId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record WorkflowRunSummaryResponse(
    Guid Id,
    Guid WorkflowVersionId,
    string Status,
    bool IsPreview,
    bool SafeModeApplied,
    string InputSafeSummary,
    Guid RequestedByUserId,
    Guid? AiTraceRecordId,
    DateTimeOffset StartedAt);

public sealed record WorkflowRunDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid WorkflowVersionId,
    string Status,
    bool IsPreview,
    bool SafeModeApplied,
    bool PartialCompletion,
    string InputSafeSummaryJson,
    string? OutputSafeSummaryJson,
    string? StepResultsJson,
    string? InheritedRiskSnapshotJson,
    string? RuntimeTrustRecalculationJson,
    string? RecommendationArtifactIdsJson,
    string? ReviewTaskArtifactIdsJson,
    Guid? AuditRecordId,
    Guid? AiTraceRecordId,
    Guid RequestedByUserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<SafeModeEventSummaryResponse> SafeModeEvents,
    IReadOnlyCollection<Guid> ChildAgentRunIds,
    IReadOnlyCollection<Guid> ChildToolRunIds);
