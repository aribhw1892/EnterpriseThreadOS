using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Artifacts;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.WorkflowRuntime;
using ETOS.Backend.Workflows;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests.Fixtures;

internal static class WorkflowExecutionTestSupport
{
    internal static WebApplicationFactory<Program> CreateApplication(
        AgentExecutionTestSupport.RecordingGraphMemoryService? graphMemory = null)
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
                        ["AgentRuntime:BaseUrl"] = "http://agent-runtime.test",
                        ["WorkflowRuntime:AdapterKey"] = WorkflowRuntimeAdapterKeys.InProcess,
                        ["WorkflowRuntime:EnableDaprHost"] = "false"
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

    internal static WebApplicationFactory<Program> CreateDaprApplication(
        AgentExecutionTestSupport.RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddJsonFile("appsettings.DaprWorkflow.json", optional: true);
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ReferencePackages:RootPath"] = packagesRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false",
                        ["AgentRuntime:BaseUrl"] = "http://agent-runtime.test",
                        ["WorkflowRuntime:AdapterKey"] = WorkflowRuntimeAdapterKeys.Dapr,
                        ["WorkflowRuntime:EnableDaprHost"] = "true"
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

    internal static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedWorkflowAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string workflowKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == WorkflowDefinitionArtifactTypes.WorkflowVersion)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.WorkflowKey, workflowKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published workflow '{workflowKey}' was not found for tenant '{tenantId}'.");
    }

    internal static async Task<CreateWorkflowDefinitionResponse> CreateWorkflowAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        CreateWorkflowDefinitionRequest request)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/workflows")
        {
            Content = JsonContent.Create(request)
        };
        AgentExecutionTestSupport.AddTenantHeaders(httpRequest, tenantId, userId);

        var response = await client.SendAsync(httpRequest);
        var created = await response.Content.ReadFromJsonAsync<CreateWorkflowDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    internal static async Task<MarkWorkflowDefinitionReadyResponse> MarkWorkflowReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/workflows/{artifactId}/versions/{versionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ready = await response.Content.ReadFromJsonAsync<MarkWorkflowDefinitionReadyResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ready);
        return ready;
    }

    internal static async Task<PublishWorkflowDefinitionResponse> PublishWorkflowAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/workflows/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by workflow test."))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishWorkflowDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
    }

    internal static async Task<WorkflowExecutionResponse> ExecuteWorkflowAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string? structuredInputJson = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/workflows/{artifactId}/versions/{versionId}/execute")
        {
            Content = JsonContent.Create(new WorkflowExecutionRequest(structuredInputJson))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var execution = await response.Content.ReadFromJsonAsync<WorkflowExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        return execution;
    }

    internal static async Task<WorkflowExecutionResponse> PreviewWorkflowAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string? structuredInputJson = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/workflows/{artifactId}/versions/{versionId}/preview")
        {
            Content = JsonContent.Create(new WorkflowExecutionRequest(structuredInputJson))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var execution = await response.Content.ReadFromJsonAsync<WorkflowExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        return execution;
    }
}
