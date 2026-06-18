using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class DevelopmentDemoDataCleanerTests
{
    [Fact]
    public async Task CleanDemoData_RemovesTenantOperationalRecordsButPreservesIdentity()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        await AddTenantAdminAccessAsync(client, tenant.Id, adminUserId);
        await CreateOntologyAsync(client, tenant.Id, adminUserId, "1.0.0");

        using var cleanRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/development/clean-demo-data")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(cleanRequest, tenant.Id, adminUserId);

        var cleanResponse = await client.SendAsync(cleanRequest);
        var cleanBody = await cleanResponse.Content.ReadAsStringAsync();
        Assert.True(cleanResponse.StatusCode == HttpStatusCode.OK, cleanBody);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();

        Assert.Equal(0, await dbContext.OntologyVersions.CountAsync(version => version.TenantId == tenant.Id));
        Assert.Equal(1, await dbContext.Tenants.CountAsync(item => item.Id == tenant.Id));
        Assert.Equal(1, await dbContext.Users.CountAsync(user => user.Id == adminUserId));
        Assert.True(await dbContext.TenantMemberships.AnyAsync(membership => membership.TenantId == tenant.Id));
    }

    private static WebApplicationFactory<Program> CreateApplication()
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
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
            Content = JsonContent.Create(new CreateUserRequest(userId, email, email, email, "local-password"))
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

    private static async Task AddTenantAdminAccessAsync(HttpClient client, Guid tenantId, Guid userId)
    {
        var role = await CreateRoleAsync(client, tenantId, userId, "Admin");
        await CreateMembershipAsync(client, tenantId, userId, userId, role.Id);
        await CreateGrantAsync(client, tenantId, userId, userId, IdentityPermissions.Wildcard);
    }

    private static async Task<TenantRoleResponse> CreateRoleAsync(HttpClient client, Guid tenantId, Guid userId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/roles")
        {
            Content = JsonContent.Create(new CreateTenantRoleRequest(name, null))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var role = await response.Content.ReadFromJsonAsync<TenantRoleResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(role);
        return role;
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid tenantId, Guid adminUserId, Guid userId, Guid roleId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/memberships")
        {
            Content = JsonContent.Create(new CreateTenantMembershipRequest(userId, roleId, null))
        };
        AddTenantHeaders(request, tenantId, adminUserId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task CreateGrantAsync(HttpClient client, Guid tenantId, Guid adminUserId, Guid userId, string permissionKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/grants")
        {
            Content = JsonContent.Create(new CreateAccessGrantRequest(
                userId,
                permissionKey,
                AccessGrantKind.Temporary,
                DateTimeOffset.UtcNow.AddHours(1),
                "Temporary test grant."))
        };
        AddTenantHeaders(request, tenantId, adminUserId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task CreateOntologyAsync(HttpClient client, Guid tenantId, Guid userId, string versionLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/versions")
        {
            Content = JsonContent.Create(new CreateOntologyVersionRequest(
                "canonical-manufacturing",
                versionLabel,
                "Canonical manufacturing ontology.",
                [
                    new CreateObjectTypeDefinitionRequest("part", "Part", "Source-owned part.", """["partNumber","revision"]""", "Part identity.")
                ],
                [],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
    }
}
