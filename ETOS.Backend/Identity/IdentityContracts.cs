namespace ETOS.Backend.Identity;

public static class IdentityPermissions
{
    public const string TenantAccess = "tenant.access";
    public const string IdentityAdmin = "identity.admin";
    public const string Wildcard = "*";
}

public static class TenantHeaderNames
{
    public const string UserId = "X-ETOS-User-Id";
    public const string TenantId = "X-ETOS-Tenant-Id";
}

public sealed record TenantResponse(
    Guid Id,
    string Identifier,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateTenantRequest(
    string Identifier,
    string Name,
    string? Description);

public sealed record UserResponse(
    Guid Id,
    string UserName,
    string Email,
    string? DisplayName,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(
    Guid? Id,
    string UserName,
    string Email,
    string? DisplayName,
    string? Password);

public sealed record TenantRoleResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record CreateTenantRoleRequest(
    string Name,
    string? Description);

public sealed record TenantMembershipResponse(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string UserName,
    Guid TenantRoleId,
    string RoleName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record CreateTenantMembershipRequest(
    Guid UserId,
    Guid TenantRoleId,
    DateTimeOffset? ExpiresAt);

public sealed record PermissionResponse(
    Guid Id,
    string Key,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record CreatePermissionRequest(
    string Key,
    string? Description);

public sealed record AssignRolePermissionRequest(Guid PermissionId);

public sealed record TenantRolePermissionResponse(
    Guid Id,
    Guid TenantId,
    Guid TenantRoleId,
    Guid PermissionId,
    string PermissionKey,
    DateTimeOffset CreatedAt);

public sealed record AccessGrantResponse(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string UserName,
    string PermissionKey,
    AccessGrantKind Kind,
    DateTimeOffset? ExpiresAt,
    string Justification,
    DateTimeOffset CreatedAt);

public sealed record CreateAccessGrantRequest(
    Guid UserId,
    string PermissionKey,
    AccessGrantKind Kind,
    DateTimeOffset? ExpiresAt,
    string? Justification);

public sealed record AccessRequestResponse(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string UserName,
    string PermissionKey,
    string Reason,
    AccessRequestStatus Status,
    DateTimeOffset CreatedAt);

public sealed record CreateAccessRequestRequest(
    Guid UserId,
    string PermissionKey,
    string Reason);

public sealed record AccessDenialResponse(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    string Action,
    string Reason,
    string SafeSummary,
    DateTimeOffset CreatedAt);

public sealed record ProblemResponse(string Error);
