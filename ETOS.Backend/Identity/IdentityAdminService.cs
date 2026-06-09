using System.Security.Claims;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Identity;

public interface IIdentityAdminService
{
    Task<IReadOnlyCollection<TenantResponse>> ListTenantsAsync(CancellationToken cancellationToken);

    Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserResponse>> ListUsersAsync(CancellationToken cancellationToken);

    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PermissionResponse>> ListPermissionsAsync(CancellationToken cancellationToken);

    Task<PermissionResponse> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TenantRoleResponse>> ListRolesAsync(CancellationToken cancellationToken);

    Task<TenantRoleResponse> CreateRoleAsync(CreateTenantRoleRequest request, CancellationToken cancellationToken);

    Task<TenantRolePermissionResponse> AssignRolePermissionAsync(Guid roleId, AssignRolePermissionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TenantMembershipResponse>> ListMembershipsAsync(CancellationToken cancellationToken);

    Task<TenantMembershipResponse> CreateMembershipAsync(CreateTenantMembershipRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AccessGrantResponse>> ListGrantsAsync(CancellationToken cancellationToken);

    Task<AccessGrantResponse> CreateGrantAsync(CreateAccessGrantRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AccessRequestResponse>> ListAccessRequestsAsync(CancellationToken cancellationToken);

    Task<AccessRequestResponse> CreateAccessRequestAsync(CreateAccessRequestRequest request, CancellationToken cancellationToken);
}

public sealed class RequestValidationException(string message) : Exception(message);

public sealed class IdentityAdminService(
    EnterpriseThreadDbContext dbContext,
    UserManager<EtosUser> userManager,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IHttpContextAccessor httpContextAccessor) : IIdentityAdminService
{
    public async Task<IReadOnlyCollection<TenantResponse>> ListTenantsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(tenant => tenant.Identifier)
            .Select(tenant => new TenantResponse(
                tenant.Id,
                tenant.Identifier,
                tenant.Name,
                tenant.Description,
                tenant.IsActive,
                tenant.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.Identifier, "Tenant identifier is required.");
        RequireText(request.Name, "Tenant name is required.");

        var normalizedIdentifier = Normalize(request.Identifier);
        var exists = await dbContext.Tenants.AnyAsync(tenant => tenant.NormalizedIdentifier == normalizedIdentifier, cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Tenant identifier already exists.");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Identifier = request.Identifier.Trim(),
            NormalizedIdentifier = normalizedIdentifier,
            Name = request.Name.Trim(),
            Description = TrimOptional(request.Description),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Tenants.Add(tenant);

        var currentUserId = GetCurrentUserId();
        if (currentUserId is not null && await dbContext.Users.AnyAsync(user => user.Id == currentUserId.Value, cancellationToken))
        {
            var adminPermission = await EnsurePermissionAsync(IdentityPermissions.IdentityAdmin, "Manage tenant identity and access.", cancellationToken);
            var wildcardPermission = await EnsurePermissionAsync(IdentityPermissions.Wildcard, "Tenant administrator wildcard permission.", cancellationToken);
            var adminRole = new TenantRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Tenant Admin",
                NormalizedName = Normalize("Tenant Admin"),
                Description = "Default tenant administrator role.",
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.TenantRoles.Add(adminRole);
            dbContext.TenantMemberships.Add(new TenantMembership
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = currentUserId.Value,
                TenantRoleId = adminRole.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
            dbContext.TenantRolePermissions.Add(new TenantRolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                TenantRoleId = adminRole.Id,
                PermissionId = adminPermission.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
            dbContext.TenantRolePermissions.Add(new TenantRolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                TenantRoleId = adminRole.Id,
                PermissionId = wildcardPermission.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToTenantResponse(tenant);
    }

    public async Task<IReadOnlyCollection<UserResponse>> ListUsersAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .Select(user => new UserResponse(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                user.DisplayName,
                user.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.UserName, "User name is required.");
        RequireText(request.Email, "Email is required.");

        var user = new EtosUser
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            DisplayName = TrimOptional(request.DisplayName),
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = string.IsNullOrWhiteSpace(request.Password)
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new RequestValidationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        return ToUserResponse(user);
    }

    public async Task<IReadOnlyCollection<PermissionResponse>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Key)
            .Select(permission => new PermissionResponse(
                permission.Id,
                permission.Key,
                permission.Description,
                permission.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PermissionResponse> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.Key, "Permission key is required.");

        var permission = await EnsurePermissionAsync(request.Key, TrimOptional(request.Description), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToPermissionResponse(permission);
    }

    public async Task<IReadOnlyCollection<TenantRoleResponse>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.roles.list", cancellationToken);

        return await dbContext.TenantRoles
            .AsNoTracking()
            .Where(role => role.TenantId == context.TenantId)
            .OrderBy(role => role.Name)
            .Select(role => new TenantRoleResponse(
                role.Id,
                role.TenantId,
                role.Name,
                role.Description,
                role.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantRoleResponse> CreateRoleAsync(CreateTenantRoleRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.roles.create", cancellationToken);
        RequireText(request.Name, "Role name is required.");

        var normalizedName = Normalize(request.Name);
        var exists = await dbContext.TenantRoles.AnyAsync(
            role => role.TenantId == context.TenantId && role.NormalizedName == normalizedName,
            cancellationToken);

        if (exists)
        {
            throw new RequestValidationException("Role already exists for this tenant.");
        }

        var role = new TenantRole
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Description = TrimOptional(request.Description),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TenantRoles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToRoleResponse(role);
    }

    public async Task<TenantRolePermissionResponse> AssignRolePermissionAsync(Guid roleId, AssignRolePermissionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.role_permissions.assign", cancellationToken);

        var roleExists = await dbContext.TenantRoles.AnyAsync(role => role.Id == roleId && role.TenantId == context.TenantId, cancellationToken);
        if (!roleExists)
        {
            throw new RequestValidationException("Role was not found in the active tenant.");
        }

        var permission = await dbContext.Permissions.SingleOrDefaultAsync(candidate => candidate.Id == request.PermissionId, cancellationToken)
            ?? throw new RequestValidationException("Permission was not found.");

        var exists = await dbContext.TenantRolePermissions.AnyAsync(
            rolePermission => rolePermission.TenantId == context.TenantId
                && rolePermission.TenantRoleId == roleId
                && rolePermission.PermissionId == permission.Id,
            cancellationToken);

        if (exists)
        {
            throw new RequestValidationException("Permission is already assigned to this role.");
        }

        var assignment = new TenantRolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            TenantRoleId = roleId,
            PermissionId = permission.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TenantRolePermissions.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TenantRolePermissionResponse(
            assignment.Id,
            assignment.TenantId,
            assignment.TenantRoleId,
            assignment.PermissionId,
            permission.Key,
            assignment.CreatedAt);
    }

    public async Task<IReadOnlyCollection<TenantMembershipResponse>> ListMembershipsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.memberships.list", cancellationToken);

        return await MembershipsQuery(context.TenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantMembershipResponse> CreateMembershipAsync(CreateTenantMembershipRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.memberships.create", cancellationToken);

        var userExists = await dbContext.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new RequestValidationException("User was not found.");
        }

        var roleExists = await dbContext.TenantRoles.AnyAsync(
            role => role.Id == request.TenantRoleId && role.TenantId == context.TenantId,
            cancellationToken);

        if (!roleExists)
        {
            throw new RequestValidationException("Role was not found in the active tenant.");
        }

        if (request.ExpiresAt is not null && request.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new RequestValidationException("Membership expiration must be in the future.");
        }

        var exists = await dbContext.TenantMemberships.AnyAsync(
            membership => membership.TenantId == context.TenantId
                && membership.UserId == request.UserId
                && membership.TenantRoleId == request.TenantRoleId,
            cancellationToken);

        if (exists)
        {
            throw new RequestValidationException("Membership already exists.");
        }

        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = request.UserId,
            TenantRoleId = request.TenantRoleId,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TenantMemberships.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await MembershipsQuery(context.TenantId)
            .SingleAsync(candidate => candidate.Id == membership.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccessGrantResponse>> ListGrantsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.grants.list", cancellationToken);

        return await GrantsQuery(context.TenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccessGrantResponse> CreateGrantAsync(CreateAccessGrantRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.grants.create", cancellationToken);
        RequireText(request.PermissionKey, "Permission key is required.");

        if (!await dbContext.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken))
        {
            throw new RequestValidationException("User was not found.");
        }

        if (request.Kind == AccessGrantKind.Temporary && (request.ExpiresAt is null || request.ExpiresAt <= DateTimeOffset.UtcNow))
        {
            throw new RequestValidationException("Temporary grants require a future expiration.");
        }

        if (request.Kind == AccessGrantKind.Permanent && string.IsNullOrWhiteSpace(request.Justification))
        {
            throw new RequestValidationException("Permanent grants require justification metadata.");
        }

        var grant = new AccessGrant
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = request.UserId,
            PermissionKey = request.PermissionKey.Trim(),
            NormalizedPermissionKey = Normalize(request.PermissionKey),
            Kind = request.Kind,
            ExpiresAt = request.Kind == AccessGrantKind.Permanent ? null : request.ExpiresAt,
            Justification = string.IsNullOrWhiteSpace(request.Justification) ? "Temporary access grant." : request.Justification.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AccessGrants.Add(grant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GrantsQuery(context.TenantId)
            .SingleAsync(candidate => candidate.Id == grant.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccessRequestResponse>> ListAccessRequestsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireTenantAdminAsync("identity.access_requests.list", cancellationToken);

        return await AccessRequestsQuery(context.TenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccessRequestResponse> CreateAccessRequestAsync(CreateAccessRequestRequest request, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("identity.access_requests.create", cancellationToken);
        RequireText(request.PermissionKey, "Permission key is required.");
        RequireText(request.Reason, "Reason is required.");

        if (request.UserId != context.UserId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "identity.access_requests.create",
                "access_request_user_mismatch",
                "Users may only create access requests for themselves.",
                cancellationToken);
            throw new TenantAccessDeniedException("Users may only create access requests for themselves.");
        }

        var accessRequest = new AccessRequest
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = request.UserId,
            PermissionKey = request.PermissionKey.Trim(),
            NormalizedPermissionKey = Normalize(request.PermissionKey),
            Reason = request.Reason.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AccessRequests.Add(accessRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await AccessRequestsQuery(context.TenantId)
            .SingleAsync(candidate => candidate.Id == accessRequest.Id, cancellationToken);
    }

    private async Task<ActiveTenantContext> RequireTenantAdminAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.IdentityAdmin, cancellationToken);

        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {IdentityPermissions.IdentityAdmin} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks identity administration permission.");
        }

        return context;
    }

    private async Task<Permission> EnsurePermissionAsync(string key, string? description, CancellationToken cancellationToken)
    {
        var normalizedKey = Normalize(key);
        var existing = await dbContext.Permissions.SingleOrDefaultAsync(permission => permission.NormalizedKey == normalizedKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Key = key.Trim(),
            NormalizedKey = normalizedKey,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Permissions.Add(permission);
        return permission;
    }

    private IQueryable<TenantMembershipResponse> MembershipsQuery(Guid tenantId)
    {
        return dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId)
            .Join(dbContext.Users, membership => membership.UserId, user => user.Id, (membership, user) => new { membership, user })
            .Join(dbContext.TenantRoles, pair => pair.membership.TenantRoleId, role => role.Id, (pair, role) => new { pair.membership, pair.user, role })
            .OrderBy(pair => pair.user.UserName)
            .Select(pair => new TenantMembershipResponse(
                pair.membership.Id,
                pair.membership.TenantId,
                pair.membership.UserId,
                pair.user.UserName ?? string.Empty,
                pair.membership.TenantRoleId,
                pair.role.Name,
                pair.membership.IsActive,
                pair.membership.CreatedAt,
                pair.membership.ExpiresAt));
    }

    private IQueryable<AccessGrantResponse> GrantsQuery(Guid tenantId)
    {
        return dbContext.AccessGrants
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId)
            .Join(dbContext.Users, grant => grant.UserId, user => user.Id, (grant, user) => new { grant, user })
            .OrderBy(pair => pair.grant.PermissionKey)
            .Select(pair => new AccessGrantResponse(
                pair.grant.Id,
                pair.grant.TenantId,
                pair.grant.UserId,
                pair.user.UserName ?? string.Empty,
                pair.grant.PermissionKey,
                pair.grant.Kind,
                pair.grant.ExpiresAt,
                pair.grant.Justification,
                pair.grant.CreatedAt));
    }

    private IQueryable<AccessRequestResponse> AccessRequestsQuery(Guid tenantId)
    {
        return dbContext.AccessRequests
            .AsNoTracking()
            .Where(accessRequest => accessRequest.TenantId == tenantId)
            .Join(dbContext.Users, accessRequest => accessRequest.UserId, user => user.Id, (accessRequest, user) => new { accessRequest, user })
            .OrderByDescending(pair => pair.accessRequest.CreatedAt)
            .Select(pair => new AccessRequestResponse(
                pair.accessRequest.Id,
                pair.accessRequest.TenantId,
                pair.accessRequest.UserId,
                pair.user.UserName ?? string.Empty,
                pair.accessRequest.PermissionKey,
                pair.accessRequest.Reason,
                pair.accessRequest.Status,
                pair.accessRequest.CreatedAt));
    }

    private Guid? GetCurrentUserId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static TenantResponse ToTenantResponse(Tenant tenant)
    {
        return new TenantResponse(tenant.Id, tenant.Identifier, tenant.Name, tenant.Description, tenant.IsActive, tenant.CreatedAt);
    }

    private static UserResponse ToUserResponse(EtosUser user)
    {
        return new UserResponse(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, user.DisplayName, user.CreatedAt);
    }

    private static PermissionResponse ToPermissionResponse(Permission permission)
    {
        return new PermissionResponse(permission.Id, permission.Key, permission.Description, permission.CreatedAt);
    }

    private static TenantRoleResponse ToRoleResponse(TenantRole role)
    {
        return new TenantRoleResponse(role.Id, role.TenantId, role.Name, role.Description, role.CreatedAt);
    }

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(message);
        }
    }

    private static string? TrimOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
