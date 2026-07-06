using ETOS.Backend.Tenancy;

namespace ETOS.Backend.ToolRegistry;

public sealed class ToolRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ToolDefinitionVersionId { get; set; }
    public Guid? ConnectorDefinitionVersionId { get; set; }
    public Guid? ParentAgentRunId { get; set; }
    public Guid? ParentWorkflowRunId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public required string Status { get; set; }
    public bool IsDryRun { get; set; }
    public required string InputSafeSummaryJson { get; set; }
    public string? OutputSafeSummaryJson { get; set; }
    public string? ValidationResultJson { get; set; }
    public string? CompatibilityNotesJson { get; set; }
    public string? ErrorSafeSummary { get; set; }
    public string? ConnectorCredentialSafeSummaryJson { get; set; }
    public Guid? RetrievalRunId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public Guid? AiTraceRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record ToolRunSummaryResponse(
    Guid Id,
    Guid ToolDefinitionVersionId,
    string Status,
    bool IsDryRun,
    string InputSafeSummary,
    Guid RequestedByUserId,
    Guid? AiTraceRecordId,
    DateTimeOffset CreatedAt);

public sealed record ToolRunDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid ToolDefinitionVersionId,
    Guid? ConnectorDefinitionVersionId,
    Guid? ParentAgentRunId,
    Guid? ParentWorkflowRunId,
    string Status,
    bool IsDryRun,
    string InputSafeSummaryJson,
    string? OutputSafeSummaryJson,
    string? ValidationResultJson,
    string? CompatibilityNotesJson,
    string? ErrorSafeSummary,
    string? ConnectorCredentialSafeSummaryJson,
    Guid? RetrievalRunId,
    Guid? AuditRecordId,
    Guid? AiTraceRecordId,
    Guid RequestedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
