using ETOS.Backend.Tenancy;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace ETOS.Backend.Identity;

public sealed class EtosUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EtosIdentityRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Tenant
{
    public Guid Id { get; set; }

    public required string Identifier { get; set; }

    public required string NormalizedIdentifier { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<TenantMembership> Memberships { get; set; } = [];

    public List<TenantRole> Roles { get; set; } = [];
}

public sealed class TenantRole : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<TenantMembership> Memberships { get; set; } = [];

    public List<TenantRolePermission> RolePermissions { get; set; } = [];
}

public sealed class TenantMembership : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }

    public EtosUser? User { get; set; }

    public Guid TenantRoleId { get; set; }

    public TenantRole? TenantRole { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class Permission
{
    public Guid Id { get; set; }

    public required string Key { get; set; }

    public required string NormalizedKey { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<TenantRolePermission> RolePermissions { get; set; } = [];
}

public sealed class TenantRolePermission : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid TenantRoleId { get; set; }

    public TenantRole? TenantRole { get; set; }

    public Guid PermissionId { get; set; }

    public Permission? Permission { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AccessGrant : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }

    public EtosUser? User { get; set; }

    public required string PermissionKey { get; set; }

    public required string NormalizedPermissionKey { get; set; }

    public AccessGrantKind Kind { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public required string Justification { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AccessRequest : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }

    public EtosUser? User { get; set; }

    public required string PermissionKey { get; set; }

    public required string NormalizedPermissionKey { get; set; }

    public required string Reason { get; set; }

    public AccessRequestStatus Status { get; set; } = AccessRequestStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AccessDenialRecord
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public required string Action { get; set; }

    public required string Reason { get; set; }

    public required string SafeSummary { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EtosTenantInfo : TenantInfo
{
    public Guid TenantId { get; init; }
}

public enum AccessGrantKind
{
    Temporary = 0,
    Permanent = 1
}

public enum AccessRequestStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}
