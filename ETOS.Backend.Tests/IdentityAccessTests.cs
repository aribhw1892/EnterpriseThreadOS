using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class IdentityAccessTests
{
    [Fact]
    public async Task TenantScopedEndpointDeniesCrossTenantAccessAndRecordsAudit()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");

        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-b");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/identity/roles");
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var denial = await dbContext.AccessDenialRecords.SingleAsync();

        Assert.Equal(otherTenant.Id, denial.TenantId);
        Assert.Equal(adminUserId, denial.UserId);
        Assert.Equal("tenant_access_denied", denial.Reason);
        Assert.DoesNotContain("Password", denial.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(tenant.Id, denial.TenantId);
    }

    [Fact]
    public async Task PermanentGrantRequiresJustification()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/grants")
        {
            Content = JsonContent.Create(new CreateAccessGrantRequest(
                adminUserId,
                IdentityPermissions.IdentityAdmin,
                AccessGrantKind.Permanent,
                null,
                null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenant.Id.ToString());

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        Assert.NotNull(problem);
        Assert.Contains("Permanent grants require justification", problem.Error);
    }

    [Fact]
    public async Task TemporaryGrantRequiresFutureExpiration()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/grants")
        {
            Content = JsonContent.Create(new CreateAccessGrantRequest(
                adminUserId,
                IdentityPermissions.IdentityAdmin,
                AccessGrantKind.Temporary,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenant.Id.ToString());

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        Assert.NotNull(problem);
        Assert.Contains("Temporary grants require a future expiration", problem.Error);
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
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
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
}
