using System.Text.Json.Serialization;

namespace ETOS.Backend.Governance;

public sealed class AuditRecord
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public required string Action { get; set; }

    public AuditResult Result { get; set; }

    public string? Reason { get; set; }

    public string? SourceObjectType { get; set; }

    public string? SourceObjectId { get; set; }

    public string? PolicyName { get; set; }

    public string? PolicyVersion { get; set; }

    public string? CorrelationId { get; set; }

    public required string SafeSummary { get; set; }

    public AuditRetentionCategory RetentionCategory { get; set; } = AuditRetentionCategory.Operational;

    public DateTimeOffset? RetainUntil { get; set; }

    public bool IsArchiveEligible { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SecurityEvent> SecurityEvents { get; set; } = [];
}

public sealed class SecurityEvent
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public SecurityEventType EventType { get; set; }

    public SecurityEventSeverity Severity { get; set; }

    public required string SourceAction { get; set; }

    public string? Reason { get; set; }

    public required string SafeSummary { get; set; }

    public Guid? RelatedAuditRecordId { get; set; }

    public AuditRecord? RelatedAuditRecord { get; set; }

    public bool ReviewTaskReady { get; set; }

    public string? ReviewTaskHint { get; set; }

    public DateTimeOffset? ReviewTaskCreatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditResult
{
    Success = 0,
    Denied = 1,
    Failed = 2,
    Override = 3,
    Export = 4,
    SecurityEvent = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditRetentionCategory
{
    Operational = 0,
    Security = 1,
    Export = 2,
    Runtime = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityEventType
{
    CrossTenantAttempt = 0,
    ExportDenied = 1,
    SensitiveAccessAttempt = 2,
    OverrideUsage = 3,
    SuspiciousPolicyViolation = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityEventSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
