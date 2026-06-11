using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.IdentityResolution;
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
    IAuditRecorder auditRecorder,
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
        var artifactReadPermission = await EnsurePermissionAsync(ArtifactPermissions.Read, "Read tenant artifact registry records.", cancellationToken);
        var artifactCreatePermission = await EnsurePermissionAsync(ArtifactPermissions.Create, "Create tenant artifact registry records.", cancellationToken);
        var artifactPublishPermission = await EnsurePermissionAsync(ArtifactPermissions.Publish, "Publish tenant artifact versions.", cancellationToken);
        var artifactAdminPermission = await EnsurePermissionAsync(ArtifactPermissions.Admin, "Administer tenant artifact registry records.", cancellationToken);
        var classificationReadPermission = await EnsurePermissionAsync(ClassificationPermissions.Read, "Read tenant classification and policy records.", cancellationToken);
        var classificationManagePermission = await EnsurePermissionAsync(ClassificationPermissions.Manage, "Manage tenant classification and policy drafts.", cancellationToken);
        var classificationPublishPermission = await EnsurePermissionAsync(ClassificationPermissions.Publish, "Publish tenant classification and policy versions.", cancellationToken);
        var policyEvaluatePermission = await EnsurePermissionAsync(ClassificationPermissions.Evaluate, "Evaluate tenant policy against governed context.", cancellationToken);
        var policyAdminPermission = await EnsurePermissionAsync(ClassificationPermissions.Admin, "Administer tenant policy enforcement.", cancellationToken);
        var identityResolutionReadPermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Read, "Read tenant identity resolution records.", cancellationToken);
        var identityResolutionManagePermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Manage, "Manage tenant identity resolution rules and candidate generation.", cancellationToken);
        var identityResolutionReviewPermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Review, "Review tenant identity resolution candidates.", cancellationToken);
        var identityResolutionAdminPermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Admin, "Administer tenant identity resolution.", cancellationToken);
        var dataQualityReadPermission = await EnsurePermissionAsync(DataQualityPermissions.Read, "Read tenant data quality issues and review hooks.", cancellationToken);
        var dataQualityManagePermission = await EnsurePermissionAsync(DataQualityPermissions.Manage, "Manage tenant data quality issues and issue generation.", cancellationToken);
        var dataQualityReviewHookPermission = await EnsurePermissionAsync(DataQualityPermissions.ReviewHook, "Create data quality review hooks from governed events.", cancellationToken);
        var dataQualityAdminPermission = await EnsurePermissionAsync(DataQualityPermissions.Admin, "Administer tenant data quality records.", cancellationToken);
        var adminRole = await EnsureTenantRoleAsync(tenant.Id, cancellationToken);

        await EnsureMembershipAsync(tenant.Id, admin.Id, adminRole.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, adminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, wildcardPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactPublishPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, classificationReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, classificationManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, classificationPublishPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, policyEvaluatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, policyAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionReviewPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityReviewHookPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityAdminPermission.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBootstrapAuditAsync(tenant.Id, admin.Id, tenant.Identifier, cancellationToken);

        logger.LogInformation(
            "Seeded development identity admin {AdminEmail} for tenant {TenantIdentifier}.",
            seedOptions.AdminEmail,
            seedOptions.TenantIdentifier);
    }

    private async Task EnsureBootstrapAuditAsync(
        Guid tenantId,
        Guid userId,
        string tenantIdentifier,
        CancellationToken cancellationToken)
    {
        var bootstrapAction = "development.seed.completed";
        var exists = await dbContext.AuditRecords.AnyAsync(
            record => record.TenantId == tenantId && record.Action == bootstrapAction,
            cancellationToken);

        if (exists)
        {
            return;
        }

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                tenantId,
                userId,
                bootstrapAction,
                AuditResult.Success,
                null,
                $"Development identity seed verified for tenant '{tenantIdentifier}'.",
                SourceObjectType: nameof(Tenant),
                SourceObjectId: tenantId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);
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
