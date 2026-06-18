using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using ETOS.Backend.Recommendations;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class CapabilityDefinitionTests
{
    [Fact]
    public async Task CreateDraftWithManufacturingPackageRef()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);

        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: packageContext.ModelPackage.Id,
            ontologyVersionId: null);

        Assert.NotEqual(Guid.Empty, created.ArtifactId);
        Assert.NotEqual(Guid.Empty, created.VersionId);
        Assert.Equal("1.0.0", created.VersionLabel);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenPackageUnpublished()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var draftOntology = await CreateDraftOntologyAsync(client, packageContext.TenantId, packageContext.UserId);

        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: null,
            ontologyVersionId: draftOntology.Id);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/capabilities/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("must be published", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadySucceedsWhenPackagePublished()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: packageContext.ModelPackage.Id,
            ontologyVersionId: null);

        var ready = await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        Assert.Equal(nameof(ArtifactReadinessState.Ready), ready.ReadinessState);
    }

    [Fact]
    public async Task PublishBlockedWhileDraft()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: packageContext.ModelPackage.Id,
            ontologyVersionId: null);

        var publish = await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        Assert.False(publish.Succeeded);
        Assert.Contains(publish.BlockingReasons, reason => reason.Contains("ready", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PublishSucceedsAfterReady()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: packageContext.ModelPackage.Id,
            ontologyVersionId: null);

        await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);
        var publish = await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        Assert.True(publish.Succeeded);
        Assert.Equal(nameof(ArtifactReadinessState.Published), publish.ReadinessState);
    }

    [Fact]
    public async Task NewVersionAllowedAfterPublish()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: packageContext.ModelPackage.Id,
            ontologyVersionId: null);

        await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);
        await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        var nextVersion = await CreateVersionAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            "2.0.0",
            packageContext.ModelPackage.Id);

        Assert.Equal("2.0.0", nextVersion.VersionLabel);
    }

    [Fact]
    public async Task CrossTenantGetDenied()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var ownerContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-cap-a", "owner@example.test");
        var created = await CreateCapabilityAsync(
            client,
            ownerContext.TenantId,
            ownerContext.UserId,
            modelPackageVersionId: ownerContext.ModelPackage.Id,
            ontologyVersionId: null);

        var otherUserId = Guid.NewGuid();
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-cap-b");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/capabilities/{created.ArtifactId}/versions/{created.VersionId}");
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void ArtifactTypeSeparationFromRecommendationAndAgentProfiles()
    {
        Assert.NotEqual(
            CapabilityDefinitionArtifactTypes.CapabilityDefinition,
            RecommendationArtifactTypes.Recommendation);
        Assert.NotEqual(
            CapabilityDefinitionArtifactTypes.CapabilityDefinition,
            FutureAgentCapabilityProfileArtifactTypes.AgentCapabilityProfile);

        Assert.Throws<RequestValidationException>(() =>
            CapabilityDefinitionPayloadParser.Deserialize("""{"agentRiskLevel":"high","capabilityKey":"x","outcomeCategory":"y","outcomeSummary":"z","compatibleModelPackageVersionIds":["00000000-0000-0000-0000-000000000001"]}"""));
    }

    [Fact]
    public async Task GetReturnsResolvedPackageLabels()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            modelPackageVersionId: packageContext.ModelPackage.Id,
            ontologyVersionId: null);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/capabilities/{created.ArtifactId}/versions/{created.VersionId}");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var detail = await response.Content.ReadFromJsonAsync<CapabilityDefinitionDetailResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(detail);
        Assert.Single(detail.CompatibleModelPackages);
        Assert.Equal(packageContext.ModelPackage.Key, detail.CompatibleModelPackages.Single().Key);
        Assert.Equal(packageContext.ModelPackage.Name, detail.CompatibleModelPackages.Single().Name);
        Assert.Equal("Published", detail.CompatibleModelPackages.Single().State);
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

    private static async Task<CreateCapabilityDefinitionResponse> CreateCapabilityAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid? modelPackageVersionId,
        Guid? ontologyVersionId)
    {
        IReadOnlyCollection<Guid>? packageIds = modelPackageVersionId is null ? null : [modelPackageVersionId.Value];
        IReadOnlyCollection<Guid>? ontologyIds = ontologyVersionId is null ? null : [ontologyVersionId.Value];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/capabilities")
        {
            Content = JsonContent.Create(new CreateCapabilityDefinitionRequest(
                "BOM Impact Analysis",
                "Manufacturing capability fixture.",
                "bom-impact-analysis",
                "structural_analysis",
                "Analyze BOM change impact across released assemblies.",
                new Dictionary<string, string> { ["domain"] = "manufacturing" },
                packageIds,
                ontologyIds,
                ["bom-impact-context"],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateCapabilityDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task<OntologyVersionResponse> CreateDraftOntologyAsync(HttpClient client, Guid tenantId, Guid userId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/versions")
        {
            Content = JsonContent.Create(new CreateOntologyVersionRequest(
                $"draft-ontology-{suffix}",
                "1.0.0",
                "Draft ontology for capability readiness test.",
                [new CreateObjectTypeDefinitionRequest("part", "Part", "Source-owned part.", """["partNumber"]""", "Part identity.")],
                [],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ontology = await response.Content.ReadFromJsonAsync<OntologyVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ontology);
        return ontology;
    }

    private static async Task<CreateCapabilityDefinitionVersionResponse> CreateVersionAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        string versionLabel,
        Guid modelPackageVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/capabilities/{artifactId}/versions")
        {
            Content = JsonContent.Create(new CreateCapabilityDefinitionVersionRequest(
                versionLabel,
                "Updated capability summary.",
                "bom-impact-analysis",
                "structural_analysis",
                "Analyze BOM change impact across released assemblies.",
                new Dictionary<string, string> { ["domain"] = "manufacturing" },
                [modelPackageVersionId],
                null,
                ["bom-impact-context"],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateCapabilityDefinitionVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task<MarkCapabilityDefinitionReadyResponse> MarkReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/capabilities/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ready = await response.Content.ReadFromJsonAsync<MarkCapabilityDefinitionReadyResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ready);
        return ready;
    }

    private static async Task<PublishCapabilityDefinitionResponse> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/capabilities/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by capability test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishCapabilityDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
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
