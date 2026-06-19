using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Agents;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Artifacts;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests.Fixtures;

internal static class AgentExecutionTestSupport
{
    internal static WebApplicationFactory<Program> CreateApplication(
        RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ReferencePackages:RootPath"] = packagesRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false",
                        ["AgentRuntime:BaseUrl"] = "http://agent-runtime.test"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    if (graphMemory is not null)
                    {
                        services.RemoveAll<IGraphMemoryService>();
                        services.AddSingleton<IGraphMemoryService>(graphMemory);
                    }
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAgentRuntimeAdapter>();
                    services.AddScoped<IAgentRuntimeAdapter, MockAgentRuntimeAdapter>();
                    services.AddScoped<IAgentRuntimeAdapter, HermesRuntimeAdapter>(sp => sp.GetRequiredService<HermesRuntimeAdapter>());
                    services.AddScoped<IAgentRuntimeAdapter, LangGraphRuntimeAdapter>(sp => sp.GetRequiredService<LangGraphRuntimeAdapter>());
                });
            });
    }

    internal static WebApplicationFactory<Program> CreateApplicationWithoutRuntimeUrl()
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
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AgentRuntime:BaseUrl"] = string.Empty
                    });
                });
            });
    }

    internal static async Task<CreateAgentTypeDefinitionResponse> CreateAndPublishAnalysisAgentTypeAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/agent-types")
        {
            Content = JsonContent.Create(new CreateAgentTypeDefinitionRequest(
                "Analysis Agent Type",
                "Platform analysis agent catalog entry.",
                "analysis-agent",
                "Governed analysis and investigation agents.",
                ["object-360-context", "bom-impact-context"],
                "investigator",
                ToolRiskLevels.Medium))
        };
        AddTenantHeaders(createRequest, tenantId, userId);

        var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAgentTypeDefinitionResponse>();
        Assert.True(createResponse.StatusCode == HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
        Assert.NotNull(created);

        await MarkAgentTypeReadyAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        await PublishAgentTypeAsync(client, tenantId, userId, created.ArtifactId, created.VersionId);
        return created;
    }

    internal static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedAgentTemplateAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string templateKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published agent template '{templateKey}' was not found for tenant '{tenantId}'.");
    }

    internal static async Task<CreateAgentDefinitionResponse> CreateAgentFromTemplateAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid templateVersionId,
        string? agentKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/agents/from-template")
        {
            Content = JsonContent.Create(new CreateAgentFromTemplateRequest(
                templateVersionId,
                agentKey,
                null,
                null,
                null,
                "deterministic",
                "mock-v1"))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateAgentDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    internal static async Task<MarkAgentDefinitionReadyResponse> MarkAgentReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ready = await response.Content.ReadFromJsonAsync<MarkAgentDefinitionReadyResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ready);
        return ready;
    }

    internal static async Task<PublishAgentDefinitionResponse> PublishAgentAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by agent test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishAgentDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
    }

    internal static async Task<AgentExecutionResponse> PreviewAgentAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string queryText = "Preview governed agent context.",
        Guid? startGraphNodeId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{artifactId}/versions/{versionId}/preview")
        {
            Content = JsonContent.Create(new AgentExecutionRequest(null, queryText, startGraphNodeId ?? Guid.NewGuid()))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var execution = await response.Content.ReadFromJsonAsync<AgentExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        return execution;
    }

    internal static async Task<AgentExecutionResponse> ExecuteAgentAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string queryText = "Execute governed agent context.",
        Guid? startGraphNodeId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{artifactId}/versions/{versionId}/execute")
        {
            Content = JsonContent.Create(new AgentExecutionRequest(null, queryText, startGraphNodeId ?? Guid.NewGuid()))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var execution = await response.Content.ReadFromJsonAsync<AgentExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        return execution;
    }

    internal static async Task<AgentExecutionResponse> TestRunAgentAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string queryText = "Test-run governed agent context.",
        Guid? startGraphNodeId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{artifactId}/versions/{versionId}/test-run")
        {
            Content = JsonContent.Create(new AgentExecutionRequest(null, queryText, startGraphNodeId ?? Guid.NewGuid()))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var execution = await response.Content.ReadFromJsonAsync<AgentExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        return execution;
    }

    internal static async Task<(CreateAgentDefinitionResponse Agent, Guid TemplateVersionId)> PreparePublishedManufacturingAgentAsync(
        HttpClient client,
        WebApplicationFactory<Program> application,
        Guid tenantId,
        Guid userId,
        string? agentKey = null)
    {
        await CreateAndPublishAnalysisAgentTypeAsync(client, tenantId, userId);
        var template = await ResolvePublishedAgentTemplateAsync(application, tenantId, "manufacturing-investigator");
        var agent = await CreateAgentFromTemplateAsync(client, tenantId, userId, template.VersionId, agentKey);
        await MarkAgentReadyAsync(client, tenantId, userId, agent.ArtifactId, agent.VersionId);
        var publish = await PublishAgentAsync(client, tenantId, userId, agent.ArtifactId, agent.VersionId);
        Assert.True(publish.Succeeded);
        return (agent, template.VersionId);
    }

    internal static async Task<(CreateAgentDefinitionResponse Agent, Guid TemplateVersionId)> PrepareDraftManufacturingAgentAsync(
        HttpClient client,
        WebApplicationFactory<Program> application,
        Guid tenantId,
        Guid userId,
        string? agentKey = null)
    {
        await CreateAndPublishAnalysisAgentTypeAsync(client, tenantId, userId);
        var template = await ResolvePublishedAgentTemplateAsync(application, tenantId, "manufacturing-investigator");
        var agent = await CreateAgentFromTemplateAsync(client, tenantId, userId, template.VersionId, agentKey);
        return (agent, template.VersionId);
    }

    internal static async Task MarkAgentTypeReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agent-types/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    internal static async Task PublishAgentTypeAsync(
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
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishAgentTypeDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        Assert.True(publish.Succeeded);
    }

    internal static async Task CreateUserAsync(HttpClient client, Guid actorUserId, Guid userId, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/users")
        {
            Content = JsonContent.Create(new CreateUserRequest(userId, email, email, email, "local-password"))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    internal static async Task<TenantResponse> CreateTenantAsync(HttpClient client, Guid actorUserId, string identifier)
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

    internal static async Task CreateGrantAsync(
        HttpClient client,
        Guid tenantId,
        Guid adminUserId,
        Guid userId,
        string permissionKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/grants")
        {
            Content = JsonContent.Create(new CreateAccessGrantRequest(
                userId,
                permissionKey,
                AccessGrantKind.Permanent,
                null,
                "Agent test grant."))
        };
        AddTenantHeaders(request, tenantId, adminUserId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    internal static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }

    internal sealed class RecordingGraphMemoryService : IGraphMemoryService
    {
        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new BaseNode(
                Guid.NewGuid(),
                request.TenantId,
                request.GraphSpace,
                request.ObjectType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now));
        }

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken) =>
            Task.FromResult<BaseNode?>(null);

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new BaseRelationship(
                Guid.NewGuid(),
                request.TenantId,
                request.FromNodeId,
                request.ToNodeId,
                request.RelationshipType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now));
        }

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var startNode = new BaseNode(
                request.StartNodeId,
                request.TenantId,
                request.GraphSpace ?? GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?>(),
                null,
                now,
                now);
            return Task.FromResult(new GraphTraversalResult(startNode, [startNode], []));
        }

        public Task<GraphReadModel> ListGraphAsync(
            Guid tenantId,
            GraphSpace? graphSpace,
            string? sourceBatchId,
            IReadOnlyCollection<Guid>? nodeIds,
            IReadOnlyCollection<Guid>? relationshipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphReadModel([], []));

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> nodeIds,
            IReadOnlyCollection<Guid> relationshipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphPromotionCopyResult([], []));
    }
}
