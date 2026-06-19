using ETOS.Backend.Tenancy;

namespace ETOS.Backend.AgentRuns;

public sealed class AgentRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgentVersionId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public required string Status { get; set; }
    public bool IsPreview { get; set; }
    public bool IsDryRun { get; set; }
    public bool SafeModeApplied { get; set; }
    public required string InputSafeSummaryJson { get; set; }
    public string? OutputSafeSummaryJson { get; set; }
    public string? StructuredOutputJson { get; set; }
    public string? DerivedRiskSnapshotJson { get; set; }
    public string? FallbackUsedJson { get; set; }
    public string? ValidationResultJson { get; set; }
    public string? ErrorSafeSummary { get; set; }
    public string? GovernedContextSummaryJson { get; set; }
    public Guid? RetrievalRunId { get; set; }
    public Guid? RecommendationArtifactId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public Guid? AiTraceRecordId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record AgentRunSummaryResponse(
    Guid Id,
    Guid AgentVersionId,
    string Status,
    bool IsPreview,
    bool IsDryRun,
    string InputSafeSummary,
    Guid RequestedByUserId,
    Guid? AiTraceRecordId,
    DateTimeOffset StartedAt);

public sealed record AgentRunDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid AgentVersionId,
    string Status,
    bool IsPreview,
    bool IsDryRun,
    bool SafeModeApplied,
    string InputSafeSummaryJson,
    string? OutputSafeSummaryJson,
    string? StructuredOutputJson,
    string? DerivedRiskSnapshotJson,
    string? FallbackUsedJson,
    string? ValidationResultJson,
    string? ErrorSafeSummary,
    string? GovernedContextSummaryJson,
    Guid? RetrievalRunId,
    Guid? RecommendationArtifactId,
    Guid? AuditRecordId,
    Guid? AiTraceRecordId,
    Guid RequestedByUserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);
