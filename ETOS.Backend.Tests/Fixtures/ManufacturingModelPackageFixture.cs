using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;
using ETOS.Backend.Packages;

namespace ETOS.Backend.Tests.Fixtures;

public static class ManufacturingModelPackageFixture
{
    public sealed record PublishedPackageContext(Guid TenantId, Guid UserId, ModelPackageVersionResponse ModelPackage);

    public static async Task<PublishedPackageContext> CreatePublishedPackageAsync(
        HttpClient client,
        string tenantIdentifier = "tenant-a",
        string email = "admin@example.test")
    {
        var userId = Guid.NewGuid();
        await CreateUserAsync(client, userId, userId, email);
        var tenant = await CreateTenantAsync(client, userId, tenantIdentifier);
        await AgentExecutionTestSupport.CreateAndPublishAnalysisAgentTypeAsync(client, tenant.Id, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/development/install-reference-package")
        {
            Content = JsonContent.Create(new InstallReferencePackageRequest(ManufacturingReferencePackageKeys.PackageKey))
        };
        AddTenantHeaders(request, tenant.Id, userId);

        var response = await client.SendAsync(request);
        var installed = await response.Content.ReadFromJsonAsync<InstallReferencePackageResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(installed);

        return new PublishedPackageContext(tenant.Id, userId, installed.ModelPackage);
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

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }
}
