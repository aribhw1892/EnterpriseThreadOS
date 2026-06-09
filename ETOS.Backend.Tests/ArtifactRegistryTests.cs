using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
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

public sealed class ArtifactRegistryTests
{
    [Fact]
    public async Task PublishBlocksUntilRequiredDependenciesArePublished()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var source = await CreateArtifactAsync(client, tenant.Id, adminUserId, "query-intent", "Query Intent");
        var target = await CreateArtifactAsync(client, tenant.Id, adminUserId, "dashboard-template", "Dashboard");
        var sourceVersion = await CreateVersionAsync(client, tenant.Id, adminUserId, source.Id, "1.0.0", ArtifactReadinessState.Ready);
        var targetVersion = await CreateVersionAsync(client, tenant.Id, adminUserId, target.Id, "1.0.0", ArtifactReadinessState.Ready);

        await AddDependencyAsync(client, tenant.Id, adminUserId, target.Id, targetVersion.Id, source.Id, sourceVersion.Id);

        var blockedPublish = await PublishAsync(client, tenant.Id, adminUserId, target.Id, targetVersion.Id);

        Assert.False(blockedPublish.Succeeded);
        Assert.Equal(ArtifactReadinessState.Blocked, blockedPublish.ReadinessState);
        Assert.Contains(blockedPublish.BlockingReasons, reason => reason.Contains("Required dependency", StringComparison.OrdinalIgnoreCase));

        var sourcePublish = await PublishAsync(client, tenant.Id, adminUserId, source.Id, sourceVersion.Id);
        var targetPublish = await PublishAsync(client, tenant.Id, adminUserId, target.Id, targetVersion.Id);

        Assert.True(sourcePublish.Succeeded);
        Assert.True(targetPublish.Succeeded);
        Assert.Equal(ArtifactReadinessState.Published, targetPublish.ReadinessState);
    }

    [Fact]
    public async Task CrossTenantArtifactAccessIsDeniedAndAudited()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-b");
        var artifact = await CreateArtifactAsync(client, tenant.Id, adminUserId, "dashboard-template", "Dashboard");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/artifacts/{artifact.Id}");
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var denial = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "artifacts.get" && record.Result == AuditResult.Denied);

        Assert.Equal(otherTenant.Id, denial.TenantId);
        Assert.Equal(otherUserId, denial.UserId);
        Assert.Equal("artifact_tenant_mismatch", denial.Reason);
    }

    [Fact]
    public async Task RelationshipsRejectSelfLinksAndDependenciesReturnImpact()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var source = await CreateArtifactAsync(client, tenant.Id, adminUserId, "query-intent", "Query Intent");
        var target = await CreateArtifactAsync(client, tenant.Id, adminUserId, "dashboard-template", "Dashboard");
        var sourceVersion = await CreateVersionAsync(client, tenant.Id, adminUserId, source.Id, "1.0.0", ArtifactReadinessState.Ready);
        var targetVersion = await CreateVersionAsync(client, tenant.Id, adminUserId, target.Id, "1.0.0", ArtifactReadinessState.Ready);

        using var selfRelationship = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{source.Id}/relationships")
        {
            Content = JsonContent.Create(new CreateArtifactRelationshipRequest(
                source.Id,
                ArtifactRelationshipType.RelatedTo,
                "Self link"))
        };
        selfRelationship.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        selfRelationship.Headers.Add(TenantHeaderNames.TenantId, tenant.Id.ToString());

        var selfRelationshipResponse = await client.SendAsync(selfRelationship);

        Assert.Equal(HttpStatusCode.BadRequest, selfRelationshipResponse.StatusCode);

        await AddRelationshipAsync(client, tenant.Id, adminUserId, target.Id, source.Id);
        await AddDependencyAsync(client, tenant.Id, adminUserId, target.Id, targetVersion.Id, source.Id, sourceVersion.Id);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/artifacts/{source.Id}/versions/{sourceVersion.Id}/impact");
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenant.Id.ToString());

        var response = await client.SendAsync(request);
        var impact = await response.Content.ReadFromJsonAsync<ArtifactImpactResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(impact);
        Assert.Single(impact.Dependents);
        Assert.Equal(target.Id, impact.Dependents.Single().DependentArtifactId);
    }

    [Fact]
    public async Task ArtifactVersionsHaveNoUpdateEndpoint()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var artifact = await CreateArtifactAsync(client, tenant.Id, adminUserId, "dashboard-template", "Dashboard");
        var version = await CreateVersionAsync(client, tenant.Id, adminUserId, artifact.Id, "1.0.0", ArtifactReadinessState.Ready);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/artifacts/{artifact.Id}/versions/{version.Id}")
        {
            Content = JsonContent.Create(new { summary = "mutated" })
        };
        request.Headers.Add(TenantHeaderNames.UserId, adminUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            await response.Content.ReadAsStringAsync());
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

    private static async Task<ArtifactSummaryResponse> CreateArtifactAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        string artifactType,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/artifacts")
        {
            Content = JsonContent.Create(new CreateArtifactRequest(artifactType, name, null, null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());

        var response = await client.SendAsync(request);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactSummaryResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(artifact);

        return artifact;
    }

    private static async Task<ArtifactVersionSummaryResponse> CreateVersionAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        string versionLabel,
        ArtifactReadinessState readinessState)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{artifactId}/versions")
        {
            Content = JsonContent.Create(new CreateArtifactVersionRequest(
                versionLabel,
                $"Version {versionLabel}",
                "{}",
                readinessState,
                ArtifactCompatibilityStatus.Compatible,
                "Compatible placeholder."))
        };
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());

        var response = await client.SendAsync(request);
        var version = await response.Content.ReadFromJsonAsync<ArtifactVersionSummaryResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(version);

        return version;
    }

    private static async Task AddRelationshipAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid targetArtifactId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{artifactId}/relationships")
        {
            Content = JsonContent.Create(new CreateArtifactRelationshipRequest(
                targetArtifactId,
                ArtifactRelationshipType.Uses,
                null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task AddDependencyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        Guid requiredArtifactId,
        Guid requiredVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{artifactId}/versions/{versionId}/dependencies")
        {
            Content = JsonContent.Create(new CreateArtifactDependencyRequest(
                requiredArtifactId,
                requiredVersionId,
                ArtifactDependencyKind.DependsOn))
        };
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PublishArtifactVersionResult> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Publish from test."))
        };
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishArtifactVersionResult>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);

        return publish;
    }
}
