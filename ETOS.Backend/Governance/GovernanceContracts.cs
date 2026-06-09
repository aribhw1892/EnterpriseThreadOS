namespace ETOS.Backend.Governance;

public sealed record AuditRecordWriteRequest(
    Guid? TenantId,
    Guid? UserId,
    string Action,
    AuditResult Result,
    string? Reason,
    string SafeSummary,
    string? SourceObjectType = null,
    string? SourceObjectId = null,
    string? PolicyName = null,
    string? PolicyVersion = null,
    AuditRetentionCategory RetentionCategory = AuditRetentionCategory.Operational,
    DateTimeOffset? RetainUntil = null,
    bool IsArchiveEligible = false);

public sealed record SecurityEventWriteRequest(
    Guid? TenantId,
    Guid? UserId,
    SecurityEventType EventType,
    SecurityEventSeverity Severity,
    string SourceAction,
    string? Reason,
    string SafeSummary,
    Guid? RelatedAuditRecordId = null,
    bool ReviewTaskReady = true,
    string? ReviewTaskHint = null);

public sealed record AuditRecordResponse(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    string Action,
    AuditResult Result,
    string? Reason,
    string? SourceObjectType,
    string? SourceObjectId,
    string? PolicyName,
    string? PolicyVersion,
    string? CorrelationId,
    string SafeSummary,
    AuditRetentionCategory RetentionCategory,
    DateTimeOffset? RetainUntil,
    bool IsArchiveEligible,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt);

public sealed record SecurityEventResponse(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    SecurityEventType EventType,
    SecurityEventSeverity Severity,
    string SourceAction,
    string? Reason,
    string SafeSummary,
    Guid? RelatedAuditRecordId,
    bool ReviewTaskReady,
    string? ReviewTaskHint,
    DateTimeOffset? ReviewTaskCreatedAt,
    DateTimeOffset CreatedAt);
