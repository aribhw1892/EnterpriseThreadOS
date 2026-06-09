using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class GovernanceAuditTests
{
    [Fact]
    public async Task TenantCreationCreatesSafeAuditRecordWithRetentionPlaceholder()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var auditRecord = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "identity.tenants.create");

        Assert.Equal(tenant.Id, auditRecord.TenantId);
        Assert.Equal(adminUserId, auditRecord.UserId);
        Assert.Equal(AuditResult.Success, auditRecord.Result);
        Assert.Equal(AuditRetentionCategory.Operational, auditRecord.RetentionCategory);
        Assert.Null(auditRecord.ArchivedAt);
        Assert.DoesNotContain("Password", auditRecord.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossTenantDenialCreatesAuditAndSecurityEvent()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");

        await CreateTenantAsync(client, adminUserId, "tenant-a");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-b");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/identity/roles");
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var deniedAudit = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "identity.roles.list" && record.Result == AuditResult.Denied);
        var securityEvent = await dbContext.SecurityEvents.SingleAsync(
            candidate => candidate.RelatedAuditRecordId == deniedAudit.Id);

        Assert.Equal(otherTenant.Id, deniedAudit.TenantId);
        Assert.Equal(adminUserId, deniedAudit.UserId);
        Assert.Equal("tenant_access_denied", deniedAudit.Reason);
        Assert.Equal(SecurityEventType.CrossTenantAttempt, securityEvent.EventType);
        Assert.Equal(SecurityEventSeverity.High, securityEvent.Severity);
        Assert.True(securityEvent.ReviewTaskReady);
    }

    [Fact]
    public async Task AuditExplorerReturnsOnlyActiveTenantRecords()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");

        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-b");

        await CreateRoleAsync(client, adminUserId, tenant.Id, "Quality Admin");
        await CreateRoleAsync(client, otherUserId, otherTenant.Id, "Manufacturing Admin");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/governance/audit-records?limit=50");
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenant.Id.ToString());

        var response = await client.SendAsync(request);
        var auditRecords = await response.Content.ReadFromJsonAsync<List<AuditRecordResponse>>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(auditRecords);
        Assert.NotEmpty(auditRecords);
        Assert.All(auditRecords, record => Assert.Equal(tenant.Id, record.TenantId));
        Assert.DoesNotContain(auditRecords, record => record.TenantId == otherTenant.Id);
    }

    private static WebApplicationFactory<Program> CreateApplication()
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                });
            });
    }

    private static async Task CreateUserAsync(HttpClient client, Guid actorUserId, Guid userId, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/users")
        {
            Content = JsonContent.Create(new CreateUserRequest(
                userId,
                email,
                email,
                email,
                "local-password"))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<TenantResponse> CreateTenantAsync(HttpClient client, Guid actorUserId, string identifier)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/tenants")
        {
            Content = JsonContent.Create(new CreateTenantRequest(identifier, identifier, null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());

        var response = await client.SendAsync(request);
        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(tenant);

        return tenant;
    }

    private static async Task CreateRoleAsync(HttpClient client, Guid actorUserId, Guid tenantId, string roleName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/roles")
        {
            Content = JsonContent.Create(new CreateTenantRoleRequest(roleName, null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
