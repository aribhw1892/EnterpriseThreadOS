using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Workflows;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class WorkflowReadOnlyConstraintTests
{
    private const string ValidInputSchema =
        """{"type":"object","required":["queryText"],"properties":{"queryText":{"type":"string"}}}""";

    private const string ValidOutputSchema =
        """{"type":"object","required":["safeSummary"],"properties":{"safeSummary":{"type":"string"}}}""";

    [Fact]
    public async Task MarkReadyBlockedWhenPinnedToolWritesExternalSystem()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var writeTool = await CreatePublishedWriteToolAsync(client, application, packageContext);

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildWorkflowRequest(packageContext.ModelPackage.Id, writeTool.VersionId));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/workflows/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("writesExternalSystem", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteReadOnlyWorkflowDoesNotCreateDecisionArtifacts()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = WorkflowExecutionTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var tool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildWorkflowRequest(packageContext.ModelPackage.Id, tool.VersionId));

        await WorkflowExecutionTestSupport.MarkWorkflowReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);
        await WorkflowExecutionTestSupport.PublishWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        var execution = await WorkflowExecutionTestSupport.ExecuteWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId,
            """{"queryText":"Read-only workflow execute check."}""");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var decisions = await dbContext.Artifacts
            .Where(item => item.TenantId == packageContext.TenantId
                && item.NormalizedArtifactType == DecisionArtifactTypes.Decision.ToUpperInvariant())
            .ToListAsync();

        Assert.Empty(decisions);
        Assert.NotEqual(Guid.Empty, execution.WorkflowRunId);
    }

    private static CreateWorkflowDefinitionRequest BuildWorkflowRequest(
        Guid modelPackageVersionId,
        Guid toolVersionId)
        => new(
            "Read Only Constraint Workflow",
            "Workflow fixture for read-only constraint tests.",
            $"readonly-workflow-{Guid.NewGuid():N}"[..24],
            "Read Only Constraint Workflow",
            null,
            WorkflowScopes.Tenant,
            [
                new WorkflowStepDefinitionRequest(
                    "tool-step",
                    WorkflowStepTypes.ToolExecute,
                    WorkflowStepSafeModeBehaviors.Skip,
                    null,
                    null,
                    toolVersionId,
                    null,
                    null,
                    null,
                    null)
            ],
            null,
            null,
            null,
            [toolVersionId],
            null,
            null,
            [modelPackageVersionId],
            null,
            false,
            false,
            null,
            true,
            WorkflowStepSafeModeBehaviors.Skip,
            new WorkflowTriggerConfigRequest(true, false, null, false, null),
            null,
            null,
            null);

    private static async Task<CreateToolDefinitionResponse> CreatePublishedWriteToolAsync(
        HttpClient client,
        WebApplicationFactory<Program> application,
        ManufacturingModelPackageFixture.PublishedPackageContext packageContext)
    {
        var connector = await ResolvePublishedConnectorAsync(application, packageContext.TenantId, "mock-erp-write-item");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tools")
        {
            Content = JsonContent.Create(new CreateToolDefinitionRequest(
                "Workflow Write Tool",
                "Write tool blocked from workflow publish.",
                $"workflow-write-tool-{Guid.NewGuid():N}"[..24],
                "connector",
                ToolRiskLevels.High,
                false,
                false,
                false,
                false,
                true,
                true,
                false,
                true,
                ["tools.execute"],
                ValidInputSchema,
                ValidOutputSchema,
                ToolInternalHandlerKeys.DisabledWriteConnector,
                null,
                connector.VersionId,
                [packageContext.ModelPackage.Id],
                null,
                null,
                null,
                null,
                null,
                []))
        };
        AgentExecutionTestSupport.AddTenantHeaders(createRequest, packageContext.TenantId, packageContext.UserId);

        var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateToolDefinitionResponse>();
        Assert.True(createResponse.StatusCode == HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
        Assert.NotNull(created);

        using var readyRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(readyRequest, packageContext.TenantId, packageContext.UserId);
        var readyResponse = await client.SendAsync(readyRequest);
        Assert.True(readyResponse.StatusCode == HttpStatusCode.OK, await readyResponse.Content.ReadAsStringAsync());

        using var publishRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{created.ArtifactId}/versions/{created.VersionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by workflow read-only test."))
        };
        AgentExecutionTestSupport.AddTenantHeaders(publishRequest, packageContext.TenantId, packageContext.UserId);
        var publishResponse = await client.SendAsync(publishRequest);
        Assert.True(publishResponse.StatusCode == HttpStatusCode.OK, await publishResponse.Content.ReadAsStringAsync());

        return created;
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedToolAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string toolKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.ToolKey, toolKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published tool '{toolKey}' was not found.");
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedConnectorAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string connectorKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == ConnectorDefinitionArtifactTypes.ConnectorDefinition)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = ConnectorDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.ConnectorKey, connectorKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published connector '{connectorKey}' was not found.");
    }
}
