using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Identity;

public interface IDevelopmentIdentitySeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public sealed class DevelopmentIdentitySeeder(
    EnterpriseThreadDbContext dbContext,
    UserManager<EtosUser> userManager,
    IOptions<SeedIdentityOptions> options,
    ILogger<DevelopmentIdentitySeeder> logger) : IDevelopmentIdentitySeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seedOptions = options.Value;
        if (!seedOptions.Enabled)
        {
            return;
        }

        var admin = await EnsureAdminUserAsync(seedOptions, cancellationToken);
        var tenant = await EnsureTenantAsync(seedOptions, cancellationToken);
        var adminPermission = await EnsurePermissionAsync(IdentityPermissions.IdentityAdmin, "Manage tenant identity and access.", cancellationToken);
        var wildcardPermission = await EnsurePermissionAsync(IdentityPermissions.Wildcard, "Tenant administrator wildcard permission.", cancellationToken);
        var adminRole = await EnsureTenantRoleAsync(tenant.Id, cancellationToken);

        await EnsureMembershipAsync(tenant.Id, admin.Id, adminRole.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, adminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, wildcardPermission.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded development identity admin {AdminEmail} for tenant {TenantIdentifier}.",
            seedOptions.AdminEmail,
            seedOptions.TenantIdentifier);
    }

    private async Task<EtosUser> EnsureAdminUserAsync(SeedIdentityOptions seedOptions, CancellationToken cancellationToken)
    {
        var normalizedEmail = seedOptions.AdminEmail.Trim().ToUpperInvariant();
        var existing = await dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var admin = new EtosUser
        {
            Id = seedOptions.AdminUserId,
            UserName = seedOptions.AdminEmail.Trim(),
            Email = seedOptions.AdminEmail.Trim(),
            DisplayName = "ETOS Local Admin",
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(admin, seedOptions.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Development admin seed failed: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }

        return admin;
    }

    private async Task<Tenant> EnsureTenantAsync(SeedIdentityOptions seedOptions, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = Normalize(seedOptions.TenantIdentifier);
        var existing = await dbContext.Tenants.SingleOrDefaultAsync(tenant => tenant.NormalizedIdentifier == normalizedIdentifier, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var tenant = new Tenant
        {
            Id = seedOptions.TenantId,
            Identifier = seedOptions.TenantIdentifier.Trim(),
            NormalizedIdentifier = normalizedIdentifier,
            Name = seedOptions.TenantName.Trim(),
            Description = "Development seed tenant.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Tenants.Add(tenant);
        return tenant;
    }

    private async Task<Permission> EnsurePermissionAsync(string key, string description, CancellationToken cancellationToken)
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
            Key = key,
            NormalizedKey = normalizedKey,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Permissions.Add(permission);
        return permission;
    }

    private async Task<TenantRole> EnsureTenantRoleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var normalizedRoleName = Normalize("Tenant Admin");
        var existing = await dbContext.TenantRoles.SingleOrDefaultAsync(
            role => role.TenantId == tenantId && role.NormalizedName == normalizedRoleName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var role = new TenantRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Tenant Admin",
            NormalizedName = normalizedRoleName,
            Description = "Default tenant administrator role.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TenantRoles.Add(role);
        return role;
    }

    private async Task EnsureMembershipAsync(Guid tenantId, Guid userId, Guid tenantRoleId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.TenantMemberships.AnyAsync(
            membership => membership.TenantId == tenantId
                && membership.UserId == userId
                && membership.TenantRoleId == tenantRoleId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TenantRoleId = tenantRoleId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task EnsureRolePermissionAsync(Guid tenantId, Guid tenantRoleId, Guid permissionId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.TenantRolePermissions.AnyAsync(
            rolePermission => rolePermission.TenantId == tenantId
                && rolePermission.TenantRoleId == tenantRoleId
                && rolePermission.PermissionId == permissionId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.TenantRolePermissions.Add(new TenantRolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantRoleId = tenantRoleId,
            PermissionId = permissionId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
