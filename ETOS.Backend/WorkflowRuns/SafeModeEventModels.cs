using ETOS.Backend.Tenancy;

namespace ETOS.Backend.WorkflowRuns;

public static class SafeModeEventKinds
{
    public const string Blocked = "Blocked";
    public const string Skipped = "Skipped";
    public const string Downgraded = "Downgraded";
    public const string RuntimeTrustDowngrade = "RuntimeTrustDowngrade";
}

public sealed class SafeModeEvent : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public required string StepKey { get; set; }
    public required string EventKind { get; set; }
    public required string Reason { get; set; }
    public string? PolicyRuleKey { get; set; }
    public string? BlockedAction { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? ToolRunId { get; set; }
    public Guid? ReviewTaskArtifactId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record SafeModeEventSummaryResponse(
    Guid Id,
    string StepKey,
    string EventKind,
    string Reason,
    string? PolicyRuleKey,
    string? BlockedAction,
    Guid? AgentRunId,
    Guid? ToolRunId,
    Guid? ReviewTaskArtifactId,
    DateTimeOffset CreatedAt);
