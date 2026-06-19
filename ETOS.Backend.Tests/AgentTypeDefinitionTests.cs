using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using ETOS.Backend.ToolRegistry;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class AgentTypeDefinitionTests
{
    [Fact]
    public async Task CreateDraftWithRequiredFields()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);

        var created = await CreateAgentTypeAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            "analysis-agent");

        Assert.NotEqual(Guid.Empty, created.ArtifactId);
        Assert.Equal("1.0.0", created.VersionLabel);
    }

    [Fact]
    public async Task MarkReadySucceedsForValidPayload()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateAgentTypeAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            "analysis-agent");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-types/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var ready = await response.Content.ReadFromJsonAsync<MarkAgentTypeDefinitionReadyResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ready);
        Assert.Equal(nameof(ArtifactReadinessState.Ready), ready.ReadinessState);
    }

    [Fact]
    public async Task PublishBlockedWhileDraft()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateAgentTypeAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            "analysis-agent");

        var publish = await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        Assert.False(publish.Succeeded);
        Assert.Contains(publish.BlockingReasons, reason => reason.Contains("ready", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PublishSucceedsAfterReadyAndCrossTenantGetDenied()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var ownerContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-agent-type-a", "owner@example.test");
        var created = await CreateAgentTypeAsync(
            client,
            ownerContext.TenantId,
            ownerContext.UserId,
            "analysis-agent");

        await MarkReadyAsync(client, ownerContext.TenantId, ownerContext.UserId, created.ArtifactId, created.VersionId);
        var publish = await PublishAsync(client, ownerContext.TenantId, ownerContext.UserId, created.ArtifactId, created.VersionId);
        Assert.True(publish.Succeeded);

        var otherUserId = Guid.NewGuid();
        await AgentExecutionTestSupport.CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await AgentExecutionTestSupport.CreateTenantAsync(client, otherUserId, "tenant-agent-type-b");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agent-types/{created.ArtifactId}/versions/{created.VersionId}");
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListReturnsTenantScopedAgentTypes()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        await CreateAgentTypeAsync(client, packageContext.TenantId, packageContext.UserId, "analysis-agent");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/agent-types");
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var items = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<AgentTypeDefinitionArtifactSummaryResponse>>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(items);
        Assert.Contains(items, item => item.TypeKey == "analysis-agent");
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

    private static async Task<CreateAgentTypeDefinitionResponse> CreateAgentTypeAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        string typeKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/agent-types")
        {
            Content = JsonContent.Create(new CreateAgentTypeDefinitionRequest(
                "Analysis Agent Type",
                "Catalog type for governed analysis agents.",
                typeKey,
                "Governed analysis and investigation agents.",
                ["object-360-context"],
                "investigator",
                ToolRiskLevels.Medium))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateAgentTypeDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task MarkReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-types/{artifactId}/versions/{versionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PublishAgentTypeDefinitionResponse> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-types/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by agent type test."))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishAgentTypeDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
    }
}
