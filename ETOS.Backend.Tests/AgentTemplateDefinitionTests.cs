using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Dashboards;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class AgentTemplateDefinitionTests
{
    [Fact]
    public async Task CreateDraftWithPublishedPromptOutputSchemaAndQueryIntentStrategy()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);
        var deps = await SeedAgentTemplateDependenciesAsync(application, packageContext.TenantId, packageContext.UserId);

        var created = await CreateAgentTemplateAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            publishedCapability.VersionId,
            deps.PromptTemplateVersionId,
            deps.OutputSchemaVersionId,
            deps.QueryIntentVersionId,
            deps.RetrievalStrategyVersionId);

        Assert.NotEqual(Guid.Empty, created.ArtifactId);
        Assert.Equal("1.0.0", created.VersionLabel);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenPromptTemplateUnpublished()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);
        var deps = await SeedAgentTemplateDependenciesAsync(application, packageContext.TenantId, packageContext.UserId);
        var draftPromptId = await InsertDraftPromptTemplateVersionAsync(application, packageContext.TenantId, packageContext.UserId);

        var created = await CreateAgentTemplateAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            publishedCapability.VersionId,
            draftPromptId,
            deps.OutputSchemaVersionId,
            deps.QueryIntentVersionId,
            deps.RetrievalStrategyVersionId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-templates/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("must be published", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenOptimizationModelRefWrongType()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);
        var deps = await SeedAgentTemplateDependenciesAsync(application, packageContext.TenantId, packageContext.UserId);
        var wrongOptimizationVersionId = await InsertNonOptimizationModelVersionAsync(application, packageContext.TenantId, packageContext.UserId);

        var created = await CreateAgentTemplateAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            publishedCapability.VersionId,
            deps.PromptTemplateVersionId,
            deps.OutputSchemaVersionId,
            deps.QueryIntentVersionId,
            deps.RetrievalStrategyVersionId,
            wrongOptimizationVersionId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-templates/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(OptimizationModelDefinitionArtifactTypes.OptimizationModel, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompositionIncludesOptionalOptimizationModelRef()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, packageContext.TenantId, packageContext.UserId, packageContext.ModelPackage.Id);
        var publishedPolicy = await CreateAndPublishBusinessPolicyAsync(client, packageContext.TenantId, packageContext.UserId, publishedCapability.VersionId);
        var publishedOptimization = await CreateAndPublishOptimizationModelAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            publishedCapability.VersionId,
            publishedPolicy.VersionId);
        var deps = await SeedAgentTemplateDependenciesAsync(application, packageContext.TenantId, packageContext.UserId);

        var created = await CreateAgentTemplateAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            publishedCapability.VersionId,
            deps.PromptTemplateVersionId,
            deps.OutputSchemaVersionId,
            deps.QueryIntentVersionId,
            deps.RetrievalStrategyVersionId,
            publishedOptimization.VersionId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agent-templates/{created.ArtifactId}/versions/{created.VersionId}");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var detail = await response.Content.ReadFromJsonAsync<AgentTemplateDefinitionDetailResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(detail);
        Assert.Single(detail.ReferencedOptimizationModels);
        Assert.Equal("minimize-transport-distance", detail.ReferencedOptimizationModels.Single().OptimizationKey);
    }

    [Fact]
    public void AgentTemplateVersionDiffersFromAgentVersionArtifactType()
    {
        Assert.NotEqual(AgentTemplateDefinitionArtifactTypes.AgentTemplate, FutureAgentArtifactTypes.AgentVersion);
        Assert.NotEqual(AgentTemplateDefinitionPermissions.Read, "agents.read");
        Assert.StartsWith("agent-templates.", AgentTemplateDefinitionPermissions.Read);
    }

    [Fact]
    public async Task PublishSucceedsAfterReadyAndCrossTenantGetDenied()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var ownerContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-tpl-a", "owner@example.test");
        var publishedCapability = await CreateAndPublishCapabilityAsync(client, ownerContext.TenantId, ownerContext.UserId, ownerContext.ModelPackage.Id);
        var deps = await SeedAgentTemplateDependenciesAsync(application, ownerContext.TenantId, ownerContext.UserId);

        var created = await CreateAgentTemplateAsync(
            client,
            ownerContext.TenantId,
            ownerContext.UserId,
            ownerContext.ModelPackage.Id,
            publishedCapability.VersionId,
            deps.PromptTemplateVersionId,
            deps.OutputSchemaVersionId,
            deps.QueryIntentVersionId,
            deps.RetrievalStrategyVersionId);

        await MarkReadyAsync(client, ownerContext.TenantId, ownerContext.UserId, created.ArtifactId, created.VersionId);
        var publish = await PublishAsync(client, ownerContext.TenantId, ownerContext.UserId, created.ArtifactId, created.VersionId);
        Assert.True(publish.Succeeded);

        var otherUserId = Guid.NewGuid();
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-tpl-b");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agent-templates/{created.ArtifactId}/versions/{created.VersionId}");
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record AgentTemplateDependencySeed(
        Guid PromptTemplateVersionId,
        Guid OutputSchemaVersionId,
        Guid QueryIntentVersionId,
        Guid RetrievalStrategyVersionId);

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

    private static async Task<AgentTemplateDependencySeed> SeedAgentTemplateDependenciesAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        Guid userId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<IGovernedChatArtifactSeeder>();
        var context = new ActiveTenantContext(tenantId, tenantId.ToString(), "Test tenant", userId);
        var artifacts = await seeder.EnsurePlatformArtifactsAsync(context, CancellationToken.None);

        var intent = await dbContext.QueryIntentVersions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.IntentKey == "object-360-context");
        if (intent is null)
        {
            intent = new QueryIntentVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IntentKey = "object-360-context",
                NormalizedIntentKey = "OBJECT-360-CONTEXT",
                VersionLabel = "v1",
                NormalizedVersionLabel = "V1",
                Name = "Object 360 context",
                Summary = "Fixture query intent.",
                IntentKind = QueryIntentKind.Object360Context,
                Source = QueryIntentSource.PlatformFixed,
                IsEnabled = true,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.QueryIntentVersions.Add(intent);
        }

        var strategy = await dbContext.RetrievalStrategyVersions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.StrategyKey == "object-360-trusted-graph-documents");
        if (strategy is null)
        {
            strategy = new RetrievalStrategyVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StrategyKey = "trusted-graph-first",
                NormalizedStrategyKey = "TRUSTED-GRAPH-FIRST",
                VersionLabel = "v1",
                NormalizedVersionLabel = "V1",
                Name = "Trusted graph first",
                Summary = "Fixture retrieval strategy.",
                GraphSpace = GraphSpace.Trusted,
                RequiredTrustState = TrustState.Trusted,
                RelationshipTypesJson = "[]",
                AllowsSemanticFallback = false,
                AllowsVectorFallback = false,
                Source = QueryIntentSource.PlatformFixed,
                IsEnabled = true,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.RetrievalStrategyVersions.Add(strategy);
        }

        await dbContext.SaveChangesAsync();

        return new AgentTemplateDependencySeed(
            artifacts.PromptTemplate.VersionId,
            artifacts.ChatAnswerSchema.VersionId,
            intent.Id,
            strategy.Id);
    }

    private static async Task<CreateAgentTemplateDefinitionResponse> CreateAgentTemplateAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid modelPackageVersionId,
        Guid capabilityVersionId,
        Guid promptTemplateVersionId,
        Guid outputSchemaVersionId,
        Guid queryIntentVersionId,
        Guid retrievalStrategyVersionId,
        Guid? optimizationModelVersionId = null)
    {
        IReadOnlyCollection<Guid>? optimizationIds = optimizationModelVersionId is null ? null : [optimizationModelVersionId.Value];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/agent-templates")
        {
            Content = JsonContent.Create(new CreateAgentTemplateDefinitionRequest(
                "Manufacturing Investigator",
                "Manufacturing agent template fixture.",
                "manufacturing-investigator",
                "investigator",
                "Investigate manufacturing context with governed retrieval.",
                AgentRuntimeAdapterKeys.PydanticAi,
                [modelPackageVersionId],
                null,
                [capabilityVersionId],
                null,
                optimizationIds,
                promptTemplateVersionId,
                outputSchemaVersionId,
                queryIntentVersionId,
                retrievalStrategyVersionId,
                null,
                new Dictionary<string, string> { ["mode"] = "analysis" },
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateAgentTemplateDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
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

    private static async Task<(Guid ArtifactId, Guid VersionId)> CreateAndPublishBusinessPolicyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid capabilityVersionId)
    {
        var created = await CreateBusinessPolicyAsync(client, tenantId, userId, capabilityVersionId);
        await MarkBusinessPolicyReadyAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        await PublishBusinessPolicyAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        return (created.ArtifactId, created.VersionId);
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> CreateAndPublishOptimizationModelAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid capabilityVersionId,
        Guid policyVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/optimization-models")
        {
            Content = JsonContent.Create(new CreateOptimizationModelDefinitionRequest(
                "Minimize Transport Distance",
                "Manufacturing optimization model fixture.",
                "minimize-transport-distance",
                "minimize_distance",
                "Minimize transport distance across candidate locations.",
                new Dictionary<string, string> { ["unit"] = "kilometers" },
                new Dictionary<string, string> { ["engine"] = "metadata-only" },
                ["candidateLocations[]"],
                [capabilityVersionId],
                [policyVersionId],
                null,
                null,
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateOptimizationModelDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);

        await MarkOptimizationReadyAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        await PublishOptimizationAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        return (created.ArtifactId, created.VersionId);
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
            $"/api/admin/agent-templates/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PublishAgentTemplateDefinitionResponse> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-templates/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by agent template test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishAgentTemplateDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
    }

    private static async Task<Guid> InsertDraftPromptTemplateVersionAsync(
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
            ArtifactType = "PromptTemplateVersion",
            NormalizedArtifactType = "PROMPTTEMPLATEVERSION",
            Name = "draft-prompt-fixture",
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
            VersionLabel = "draft-v1",
            NormalizedVersionLabel = "DRAFT-V1",
            PayloadJson = """{"template":"Draft prompt"}""",
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync();

        return version.Id;
    }

    private static async Task<Guid> InsertNonOptimizationModelVersionAsync(
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

    private static async Task<CreateBusinessPolicyDefinitionResponse> CreateBusinessPolicyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid referencedCapabilityVersionId)
    {
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
                null,
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

    private static async Task MarkBusinessPolicyReadyAsync(
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
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task PublishBusinessPolicyAsync(
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
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by agent template test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task MarkOptimizationReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/optimization-models/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task PublishOptimizationAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/optimization-models/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by agent template test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by agent template test."))
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
