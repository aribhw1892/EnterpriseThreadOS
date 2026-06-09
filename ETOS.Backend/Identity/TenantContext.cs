using System.Security.Claims;
using ETOS.Backend.Governance;
using ETOS.Backend.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Identity;

public sealed record ActiveTenantContext(
    Guid TenantId,
    string Identifier,
    string Name,
    Guid UserId);

public interface ITenantContextResolver
{
    Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken);
}

public interface IAccessPermissionService
{
    Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken);
}

public interface IAccessDenialRecorder
{
    Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken);
}

public sealed class TenantAccessDeniedException(string reason) : Exception(reason);

public sealed class TenantContextResolver(
    IMultiTenantContextAccessor<EtosTenantInfo> tenantContextAccessor,
    IHttpContextAccessor httpContextAccessor,
    EnterpriseThreadDbContext dbContext,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : ITenantContextResolver
{
    public async Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var userId = GetUserId(httpContext?.User);

        if (userId is null)
        {
            await denialRecorder.RecordAsync(null, null, action, "missing_user", "No authenticated ETOS user was provided.", cancellationToken);
            throw new TenantAccessDeniedException("Missing authenticated user.");
        }

        var tenantInfo = tenantContextAccessor.MultiTenantContext?.TenantInfo
            ?? httpContext?.GetMultiTenantContext<EtosTenantInfo>()?.TenantInfo
            ?? await ResolveTenantFromHeaderAsync(httpContext, cancellationToken);
        if (tenantInfo is null || tenantInfo.TenantId == Guid.Empty)
        {
            await denialRecorder.RecordAsync(null, userId, action, "missing_tenant", "No active tenant was resolved for the request.", cancellationToken);
            throw new TenantAccessDeniedException("Missing or invalid tenant.");
        }

        var hasAccess = await permissionService.HasTenantAccessAsync(tenantInfo.TenantId, userId.Value, cancellationToken);
        if (!hasAccess)
        {
            await denialRecorder.RecordAsync(
                tenantInfo.TenantId,
                userId,
                action,
                "tenant_access_denied",
                "The authenticated user is not a member of the requested tenant and has no active tenant access grant.",
                cancellationToken);
            throw new TenantAccessDeniedException("User is not authorized for the requested tenant.");
        }

        return new ActiveTenantContext(
            tenantInfo.TenantId,
            tenantInfo.Identifier ?? tenantInfo.TenantId.ToString(),
            tenantInfo.Name ?? tenantInfo.Identifier ?? tenantInfo.TenantId.ToString(),
            userId.Value);
    }

    private static Guid? GetUserId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private async Task<EtosTenantInfo?> ResolveTenantFromHeaderAsync(HttpContext? httpContext, CancellationToken cancellationToken)
    {
        if (httpContext is null || !httpContext.Request.Headers.TryGetValue(TenantHeaderNames.TenantId, out var tenantHeader))
        {
            return null;
        }

        var tenantSelector = tenantHeader.ToString();
        var normalizedSelector = tenantSelector.Trim().ToUpperInvariant();
        var tenantId = Guid.TryParse(tenantSelector, out var parsedTenantId) ? parsedTenantId : (Guid?)null;

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.IsActive
                && (candidate.Id == tenantId || candidate.NormalizedIdentifier == normalizedSelector),
                cancellationToken);

        return tenant is null
            ? null
            : new EtosTenantInfo
            {
                Id = tenant.Id.ToString(),
                Identifier = tenant.Identifier,
                Name = tenant.Name,
                TenantId = tenant.Id
            };
    }
}

public sealed class AccessPermissionService(EnterpriseThreadDbContext dbContext) : IAccessPermissionService
{
    public async Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var hasMembership = await dbContext.TenantMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.TenantId == tenantId
                && membership.UserId == userId
                && membership.IsActive
                && (membership.ExpiresAt == null || membership.ExpiresAt > now),
                cancellationToken);

        if (hasMembership)
        {
            return true;
        }

        return await HasActiveGrantAsync(tenantId, userId, IdentityPermissions.TenantAccess, now, cancellationToken);
    }

    public async Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
    {
        var normalizedPermission = Normalize(permissionKey);
        var now = DateTimeOffset.UtcNow;

        var hasRolePermission = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId
                && membership.UserId == userId
                && membership.IsActive
                && (membership.ExpiresAt == null || membership.ExpiresAt > now))
            .Join(
                dbContext.TenantRolePermissions,
                membership => membership.TenantRoleId,
                rolePermission => rolePermission.TenantRoleId,
                (_, rolePermission) => rolePermission)
            .Join(
                dbContext.Permissions,
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.NormalizedKey)
            .AnyAsync(key => key == normalizedPermission || key == IdentityPermissions.Wildcard, cancellationToken);

        if (hasRolePermission)
        {
            return true;
        }

        return await HasActiveGrantAsync(tenantId, userId, permissionKey, now, cancellationToken);
    }

    private Task<bool> HasActiveGrantAsync(
        Guid tenantId,
        Guid userId,
        string permissionKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedPermission = Normalize(permissionKey);

        return dbContext.AccessGrants
            .AsNoTracking()
            .AnyAsync(grant =>
                grant.TenantId == tenantId
                && grant.UserId == userId
                && (grant.NormalizedPermissionKey == normalizedPermission || grant.NormalizedPermissionKey == IdentityPermissions.Wildcard)
                && (grant.ExpiresAt == null || grant.ExpiresAt > now),
                cancellationToken);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

public sealed class AccessDenialRecorder(
    EnterpriseThreadDbContext dbContext,
    IAuditRecorder auditRecorder) : IAccessDenialRecorder
{
    public async Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken)
    {
        dbContext.AccessDenialRecords.Add(new AccessDenialRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            Reason = reason,
            SafeSummary = safeSummary,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var auditRecord = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                tenantId,
                userId,
                action,
                AuditResult.Denied,
                reason,
                safeSummary,
                PolicyName: "tenant-access",
                RetentionCategory: AuditRetentionCategory.Security,
                IsArchiveEligible: true),
            cancellationToken);

        var securityClassification = ClassifySecurityEvent(reason);
        await auditRecorder.RecordSecurityEventAsync(
            new SecurityEventWriteRequest(
                tenantId,
                userId,
                securityClassification.EventType,
                securityClassification.Severity,
                action,
                reason,
                safeSummary,
                auditRecord.Id,
                ReviewTaskReady: true,
                ReviewTaskHint: "Review tenant access or permission denial."),
            cancellationToken);
    }

    private static (SecurityEventType EventType, SecurityEventSeverity Severity) ClassifySecurityEvent(string reason)
    {
        return reason switch
        {
            "tenant_access_denied" => (SecurityEventType.CrossTenantAttempt, SecurityEventSeverity.High),
            "permission_denied" => (SecurityEventType.SensitiveAccessAttempt, SecurityEventSeverity.Medium),
            "access_request_user_mismatch" => (SecurityEventType.SensitiveAccessAttempt, SecurityEventSeverity.Medium),
            "missing_tenant" => (SecurityEventType.SuspiciousPolicyViolation, SecurityEventSeverity.Low),
            "missing_user" => (SecurityEventType.SuspiciousPolicyViolation, SecurityEventSeverity.Low),
            _ => (SecurityEventType.SuspiciousPolicyViolation, SecurityEventSeverity.Medium)
        };
    }
}
