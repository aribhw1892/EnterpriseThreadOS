using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Classification;
using ETOS.Backend.Dashboards;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class BusinessPolicyDefinitionTests
{
    [Fact]
    public async Task CreateDraftReferencingPublishedCapabilityVersion()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId);

        Assert.NotEqual(Guid.Empty, created.ArtifactId);
        Assert.NotEqual(Guid.Empty, created.VersionId);
        Assert.Equal("1.0.0", created.VersionLabel);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenReferencedCapabilityUnpublished()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var draftCapability = await CreateCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: draftCapability.VersionId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/business-policies/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("must be published", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenCapabilityArtifactTypeWrong()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var wrongVersionId = await InsertNonCapabilityVersionAsync(application, packageContext.TenantId, packageContext.UserId);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: wrongVersionId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/business-policies/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(DashboardReportArtifactTypes.Dashboard, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadySucceedsWhenCapabilityPublished()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId);

        var ready = await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        Assert.Equal(nameof(ArtifactReadinessState.Ready), ready.ReadinessState);
    }

    [Fact]
    public async Task PublishBlockedWhileDraft()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId);

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
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId);

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
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId);

        await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);
        await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        var nextVersion = await CreateVersionAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            "2.0.0",
            publishedCapability.VersionId);

        Assert.Equal("2.0.0", nextVersion.VersionLabel);
    }

    [Fact]
    public async Task CrossTenantGetDenied()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var ownerContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-bp-a", "owner@example.test");
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, ownerContext.TenantId, ownerContext.UserId, ownerContext.ModelPackage.Id);
        var created = await CreateBusinessPolicyAsync(
            client,
            ownerContext.TenantId,
            ownerContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId);

        var otherUserId = Guid.NewGuid();
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-bp-b");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/business-policies/{created.ArtifactId}/versions/{created.VersionId}");
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void NamingSeparationFromClassificationPolicyVersion()
    {
        Assert.NotEqual(
            BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition,
            ClassificationPolicySeparationGuards.ClassificationPolicyEntityName);
        Assert.NotEqual(
            BusinessPolicyDefinitionPermissions.Read,
            ClassificationPermissions.Read);
        Assert.NotEqual(
            BusinessPolicyDefinitionPermissions.Admin,
            ClassificationPermissions.Admin);
        Assert.StartsWith("business-policies.", BusinessPolicyDefinitionPermissions.Read);
        Assert.StartsWith("classification.", ClassificationPermissions.Read);

        Assert.Throws<RequestValidationException>(() =>
            BusinessPolicyDefinitionPayloadParser.Deserialize(
                """{"classificationSchemeVersionId":"00000000-0000-0000-0000-000000000001","policyKey":"min-maturity-85","constraintCategory":"maturity_threshold","constraintSummary":"Require 85% maturity.","constraintRules":{"minMaturityPercent":"85"},"referencedCapabilityDefinitionVersionIds":["00000000-0000-0000-0000-000000000002"]}"""));
    }

    [Fact]
    public async Task GetReturnsResolvedCapabilityAndPackageLabels()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);

        var created = await CreateBusinessPolicyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            referencedCapabilityVersionId: publishedCapability.VersionId,
            modelPackageVersionId: packageContext.ModelPackage.Id);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/business-policies/{created.ArtifactId}/versions/{created.VersionId}");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var detail = await response.Content.ReadFromJsonAsync<BusinessPolicyDefinitionDetailResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(detail);
        Assert.Single(detail.ReferencedCapabilities);
        Assert.Equal("bom-impact-analysis", detail.ReferencedCapabilities.Single().CapabilityKey);
        Assert.Equal("Published", detail.ReferencedCapabilities.Single().ReadinessState);
        Assert.Single(detail.CompatibleModelPackages);
        Assert.Equal(packageContext.ModelPackage.Key, detail.CompatibleModelPackages.Single().Key);
        Assert.Equal(packageContext.ModelPackage.Name, detail.CompatibleModelPackages.Single().Name);
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

    private static async Task<(Guid ArtifactId, Guid VersionId)> CreateAndPublishCapabilityAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid modelPackageVersionId)
    {
        var created = await CreateCapabilityAsync(client, tenantId, userId, modelPackageVersionId);
        await MarkCapabilityReadyAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        await PublishCapabilityAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        return (created.ArtifactId, created.VersionId);
    }

    private static async Task<CreateBusinessPolicyDefinitionResponse> CreateBusinessPolicyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid referencedCapabilityVersionId,
        Guid? modelPackageVersionId = null)
    {
        IReadOnlyCollection<Guid>? packageIds = modelPackageVersionId is null ? null : [modelPackageVersionId.Value];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/business-policies")
        {
            Content = JsonContent.Create(new CreateBusinessPolicyDefinitionRequest(
                "Minimum Maturity 85%",
                "Manufacturing business constraint policy fixture.",
                "min-maturity-85",
                "maturity_threshold",
                "Require at least 85% design maturity before release approval.",
                new Dictionary<string, string> { ["minMaturityPercent"] = "85" },
                [referencedCapabilityVersionId],
                packageIds,
                null,
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateBusinessPolicyDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task<CreateBusinessPolicyDefinitionVersionResponse> CreateVersionAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        string versionLabel,
        Guid referencedCapabilityVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/business-policies/{artifactId}/versions")
        {
            Content = JsonContent.Create(new CreateBusinessPolicyDefinitionVersionRequest(
                versionLabel,
                "Updated business policy summary.",
                "min-maturity-85",
                "maturity_threshold",
                "Require at least 85% design maturity before release approval.",
                new Dictionary<string, string> { ["minMaturityPercent"] = "85" },
                [referencedCapabilityVersionId],
                null,
                null,
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateBusinessPolicyDefinitionVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task<MarkBusinessPolicyDefinitionReadyResponse> MarkReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/business-policies/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ready = await response.Content.ReadFromJsonAsync<MarkBusinessPolicyDefinitionReadyResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ready);
        return ready;
    }

    private static async Task<PublishBusinessPolicyDefinitionResponse> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/business-policies/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by business policy test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishBusinessPolicyDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
    }

    private static async Task<Guid> InsertNonCapabilityVersionAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        Guid userId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactType = DashboardReportArtifactTypes.Dashboard,
            NormalizedArtifactType = DashboardReportArtifactTypes.Dashboard.ToUpperInvariant(),
            Name = "Wrong-type fixture",
            OwnerUserId = userId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = "{}",
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync();

        return version.Id;
    }

    private static async Task<CreateCapabilityDefinitionResponse> CreateCapabilityAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid modelPackageVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/capabilities")
        {
            Content = JsonContent.Create(new CreateCapabilityDefinitionRequest(
                "BOM Impact Analysis",
                "Manufacturing capability fixture.",
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
        var created = await response.Content.ReadFromJsonAsync<CreateCapabilityDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task MarkCapabilityReadyAsync(
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
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task PublishCapabilityAsync(
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
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by business policy test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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
