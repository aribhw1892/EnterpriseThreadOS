using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class DevelopmentIdentitySeederTests
{
    [Fact]
    public async Task SeedCreatesDefaultAdminTenantAndMembership()
    {
        var seedOptions = new SeedIdentityOptions { Enabled = true };
        await using var dbContext = CreateDbContext();
        var userManager = CreateUserManager(dbContext);
        var seeder = new DevelopmentIdentitySeeder(
            dbContext,
            userManager,
            new AuditRecorder(dbContext, new HttpContextAccessor()),
            Options.Create(seedOptions),
            NullLogger<DevelopmentIdentitySeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);

        var admin = await dbContext.Users.SingleAsync(user => user.Email == "admin@etos.com");
        var tenant = await dbContext.Tenants.SingleAsync(tenant => tenant.Identifier == "local");
        var role = await dbContext.TenantRoles.SingleAsync(role => role.TenantId == tenant.Id && role.Name == "Tenant Admin");
        var membership = await dbContext.TenantMemberships.SingleAsync(item =>
            item.TenantId == tenant.Id
            && item.UserId == admin.Id
            && item.TenantRoleId == role.Id);

        Assert.Equal(seedOptions.AdminUserId, admin.Id);
        Assert.Equal(seedOptions.TenantId, tenant.Id);
        Assert.True(membership.IsActive);
        Assert.True(await userManager.CheckPasswordAsync(admin, seedOptions.AdminPassword));
        Assert.Contains(dbContext.Permissions, permission => permission.Key == IdentityPermissions.IdentityAdmin);
    }

    [Fact]
    public async Task SeedCreatesBootstrapAuditRecord()
    {
        var seedOptions = new SeedIdentityOptions { Enabled = true };
        await using var dbContext = CreateDbContext();
        var userManager = CreateUserManager(dbContext);
        var seeder = new DevelopmentIdentitySeeder(
            dbContext,
            userManager,
            new AuditRecorder(dbContext, new HttpContextAccessor()),
            Options.Create(seedOptions),
            NullLogger<DevelopmentIdentitySeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);

        var auditRecord = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "development.seed.completed");

        Assert.Equal(seedOptions.TenantId, auditRecord.TenantId);
        Assert.Equal(seedOptions.AdminUserId, auditRecord.UserId);
        Assert.Equal(AuditResult.Success, auditRecord.Result);
        Assert.Equal(AuditRetentionCategory.Operational, auditRecord.RetentionCategory);
    }

    [Fact]
    public async Task SeedIsIdempotent()
    {
        var seedOptions = new SeedIdentityOptions { Enabled = true };
        await using var dbContext = CreateDbContext();
        var userManager = CreateUserManager(dbContext);
        var seeder = new DevelopmentIdentitySeeder(
            dbContext,
            userManager,
            new AuditRecorder(dbContext, new HttpContextAccessor()),
            Options.Create(seedOptions),
            NullLogger<DevelopmentIdentitySeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "admin@etos.com"));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Email == "chat-runner@example.test"));
        Assert.Equal(1, await dbContext.Tenants.CountAsync(tenant => tenant.Identifier == "local"));
        Assert.Equal(2, await dbContext.TenantMemberships.CountAsync());
        Assert.Equal(1, await dbContext.AuditRecords.CountAsync(record => record.Action == "development.seed.completed"));
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new EnterpriseThreadDbContext(options);
    }

    private static UserManager<EtosUser> CreateUserManager(EnterpriseThreadDbContext dbContext)
    {
        var store = new UserStore(dbContext);

        return new UserManager<EtosUser>(
            store,
            Options.Create(new IdentityOptions
            {
                Password =
                {
                    RequiredLength = 8,
                    RequireDigit = false,
                    RequireLowercase = false,
                    RequireUppercase = false,
                    RequireNonAlphanumeric = false
                }
            }),
            new PasswordHasher<EtosUser>(),
            [new UserValidator<EtosUser>()],
            [new PasswordValidator<EtosUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<EtosUser>>.Instance);
    }

    private sealed class UserStore(EnterpriseThreadDbContext context) :
        Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<EtosUser, EtosIdentityRole, EnterpriseThreadDbContext, Guid>(context);
}
