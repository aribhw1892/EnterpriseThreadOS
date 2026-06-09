using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Governance;

public interface IAuditRecorder
{
    Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken);

    Task<SecurityEventResponse> RecordSecurityEventAsync(SecurityEventWriteRequest request, CancellationToken cancellationToken);
}

public interface IAuditExplorerService
{
    Task<IReadOnlyCollection<AuditRecordResponse>> ListAuditRecordsAsync(
        string? result,
        string? action,
        int? limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SecurityEventResponse>> ListSecurityEventsAsync(
        string? eventType,
        string? severity,
        int? limit,
        CancellationToken cancellationToken);
}

public sealed class AuditRecorder(
    EnterpriseThreadDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IAuditRecorder
{
    public async Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.Action, "Audit action is required.");
        RequireText(request.SafeSummary, "Audit safe summary is required.");

        var auditRecord = new AuditRecord
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            UserId = request.UserId,
            Action = TrimToMax(request.Action, 200),
            Result = request.Result,
            Reason = TrimOptionalToMax(request.Reason, 500),
            SourceObjectType = TrimOptionalToMax(request.SourceObjectType, 120),
            SourceObjectId = TrimOptionalToMax(request.SourceObjectId, 200),
            PolicyName = TrimOptionalToMax(request.PolicyName, 160),
            PolicyVersion = TrimOptionalToMax(request.PolicyVersion, 80),
            CorrelationId = GetCorrelationId(),
            SafeSummary = TrimToMax(request.SafeSummary, 1000),
            RetentionCategory = request.RetentionCategory,
            RetainUntil = request.RetainUntil,
            IsArchiveEligible = request.IsArchiveEligible,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AuditRecords.Add(auditRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAuditResponse(auditRecord);
    }

    public async Task<SecurityEventResponse> RecordSecurityEventAsync(SecurityEventWriteRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.SourceAction, "Security event source action is required.");
        RequireText(request.SafeSummary, "Security event safe summary is required.");

        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            UserId = request.UserId,
            EventType = request.EventType,
            Severity = request.Severity,
            SourceAction = TrimToMax(request.SourceAction, 200),
            Reason = TrimOptionalToMax(request.Reason, 500),
            SafeSummary = TrimToMax(request.SafeSummary, 1000),
            RelatedAuditRecordId = request.RelatedAuditRecordId,
            ReviewTaskReady = request.ReviewTaskReady,
            ReviewTaskHint = TrimOptionalToMax(request.ReviewTaskHint, 500),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.SecurityEvents.Add(securityEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSecurityEventResponse(securityEvent);
    }

    internal static AuditRecordResponse ToAuditResponse(AuditRecord record)
    {
        return new AuditRecordResponse(
            record.Id,
            record.TenantId,
            record.UserId,
            record.Action,
            record.Result,
            record.Reason,
            record.SourceObjectType,
            record.SourceObjectId,
            record.PolicyName,
            record.PolicyVersion,
            record.CorrelationId,
            record.SafeSummary,
            record.RetentionCategory,
            record.RetainUntil,
            record.IsArchiveEligible,
            record.ArchivedAt,
            record.CreatedAt);
    }

    internal static SecurityEventResponse ToSecurityEventResponse(SecurityEvent securityEvent)
    {
        return new SecurityEventResponse(
            securityEvent.Id,
            securityEvent.TenantId,
            securityEvent.UserId,
            securityEvent.EventType,
            securityEvent.Severity,
            securityEvent.SourceAction,
            securityEvent.Reason,
            securityEvent.SafeSummary,
            securityEvent.RelatedAuditRecordId,
            securityEvent.ReviewTaskReady,
            securityEvent.ReviewTaskHint,
            securityEvent.ReviewTaskCreatedAt,
            securityEvent.CreatedAt);
    }

    private string? GetCorrelationId()
    {
        var traceIdentifier = httpContextAccessor.HttpContext?.TraceIdentifier;
        return string.IsNullOrWhiteSpace(traceIdentifier) ? null : TrimToMax(traceIdentifier, 120);
    }

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }
    }

    private static string TrimToMax(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimOptionalToMax(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TrimToMax(value, maxLength);
    }
}

public sealed class AuditExplorerService(
    EnterpriseThreadDbContext dbContext,
    ETOS.Backend.Identity.ITenantContextResolver tenantContextResolver,
    ETOS.Backend.Identity.IAccessPermissionService permissionService,
    ETOS.Backend.Identity.IAccessDenialRecorder denialRecorder) : IAuditExplorerService
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    public async Task<IReadOnlyCollection<AuditRecordResponse>> ListAuditRecordsAsync(
        string? result,
        string? action,
        int? limit,
        CancellationToken cancellationToken)
    {
        var context = await RequireAuditAdminAsync("governance.audit_records.list", cancellationToken);
        var query = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.TenantId == context.TenantId);

        if (Enum.TryParse<AuditResult>(result, true, out var parsedResult))
        {
            query = query.Where(record => record.Result == parsedResult);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var trimmedAction = action.Trim();
            query = query.Where(record => record.Action == trimmedAction);
        }

        return await query
            .OrderByDescending(record => record.CreatedAt)
            .Take(NormalizeLimit(limit))
            .Select(record => AuditRecorder.ToAuditResponse(record))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SecurityEventResponse>> ListSecurityEventsAsync(
        string? eventType,
        string? severity,
        int? limit,
        CancellationToken cancellationToken)
    {
        var context = await RequireAuditAdminAsync("governance.security_events.list", cancellationToken);
        var query = dbContext.SecurityEvents
            .AsNoTracking()
            .Where(securityEvent => securityEvent.TenantId == context.TenantId);

        if (Enum.TryParse<SecurityEventType>(eventType, true, out var parsedEventType))
        {
            query = query.Where(securityEvent => securityEvent.EventType == parsedEventType);
        }

        if (Enum.TryParse<SecurityEventSeverity>(severity, true, out var parsedSeverity))
        {
            query = query.Where(securityEvent => securityEvent.Severity == parsedSeverity);
        }

        return await query
            .OrderByDescending(securityEvent => securityEvent.CreatedAt)
            .Take(NormalizeLimit(limit))
            .Select(securityEvent => AuditRecorder.ToSecurityEventResponse(securityEvent))
            .ToListAsync(cancellationToken);
    }

    private async Task<ETOS.Backend.Identity.ActiveTenantContext> RequireAuditAdminAsync(
        string action,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(
            context.TenantId,
            context.UserId,
            ETOS.Backend.Identity.IdentityPermissions.IdentityAdmin,
            cancellationToken);

        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {ETOS.Backend.Identity.IdentityPermissions.IdentityAdmin} permission.",
                cancellationToken);
            throw new ETOS.Backend.Identity.TenantAccessDeniedException("User lacks audit explorer permission.");
        }

        return context;
    }

    private static int NormalizeLimit(int? limit)
    {
        if (limit is null or <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }
}
