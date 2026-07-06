using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Workflows;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class WorkflowDefinitionTests
{
    [Fact]
    public async Task CreateDraftWithManufacturingPackageRef()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var tool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildMinimalWorkflowRequest(packageContext.ModelPackage.Id, tool.VersionId));

        Assert.NotEqual(Guid.Empty, created.ArtifactId);
        Assert.NotEqual(Guid.Empty, created.VersionId);
        Assert.Equal("1.0.0", created.VersionLabel);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenToolUnpublished()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var draftTool = await CreateDraftToolAsync(client, packageContext);

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildMinimalWorkflowRequest(packageContext.ModelPackage.Id, draftTool.VersionId));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/workflows/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("published", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadyDerivesInheritedRiskFromReferencedTools()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var tool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildMinimalWorkflowRequest(packageContext.ModelPackage.Id, tool.VersionId));

        var ready = await WorkflowExecutionTestSupport.MarkWorkflowReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        Assert.Equal(nameof(ArtifactReadinessState.Ready), ready.ReadinessState);
        Assert.NotNull(ready.DerivedCapabilityRisk);
        Assert.Equal(ToolRiskLevels.Medium, ready.DerivedCapabilityRisk.EffectiveRiskLevel);
        Assert.NotEmpty(ready.DerivedCapabilityRisk.ToolRiskContributions);
    }

    [Fact]
    public async Task PublishBlockedWhileDraft()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var tool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildMinimalWorkflowRequest(packageContext.ModelPackage.Id, tool.VersionId));

        var publish = await WorkflowExecutionTestSupport.PublishWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        Assert.False(publish.Succeeded);
        Assert.Contains(publish.BlockingReasons, reason => reason.Contains("ready", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PublishSucceedsAfterReady()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var tool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            BuildMinimalWorkflowRequest(packageContext.ModelPackage.Id, tool.VersionId));

        await WorkflowExecutionTestSupport.MarkWorkflowReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        var publish = await WorkflowExecutionTestSupport.PublishWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        Assert.True(publish.Succeeded);
        Assert.Equal(nameof(ArtifactReadinessState.Published), publish.ReadinessState);
    }

    [Fact]
    public async Task ListAndGetReturnWorkflowDetail()
    {
        await using var application = WorkflowExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var workflow = await WorkflowExecutionTestSupport.ResolvePublishedWorkflowAsync(
            application,
            packageContext.TenantId,
            "bom-impact-review");

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/workflows");
        AgentExecutionTestSupport.AddTenantHeaders(listRequest, packageContext.TenantId, packageContext.UserId);
        var listResponse = await client.SendAsync(listRequest);
        var summaries = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<WorkflowDefinitionArtifactSummaryResponse>>();

        Assert.True(listResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(summaries);
        Assert.Contains(summaries, item => item.WorkflowKey == "bom-impact-review");

        using var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/workflows/{workflow.ArtifactId}/versions/{workflow.VersionId}");
        AgentExecutionTestSupport.AddTenantHeaders(getRequest, packageContext.TenantId, packageContext.UserId);
        var getResponse = await client.SendAsync(getRequest);
        var detail = await getResponse.Content.ReadFromJsonAsync<WorkflowDefinitionDetailResponse>();

        Assert.True(getResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(detail);
        Assert.Equal("bom-impact-review", detail.WorkflowKey);
        Assert.NotEmpty(detail.Steps);
        Assert.NotNull(detail.DerivedCapabilityRisk);
    }

    private static CreateWorkflowDefinitionRequest BuildMinimalWorkflowRequest(
        Guid modelPackageVersionId,
        Guid toolVersionId)
        => new(
            "Workflow Fixture",
            "Workflow fixture for definition tests.",
            $"workflow-fixture-{Guid.NewGuid():N}"[..24],
            "Workflow Fixture",
            "Workflow fixture for definition tests.",
            WorkflowScopes.Tenant,
            [
                new WorkflowStepDefinitionRequest(
                    "graph-query",
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

    private static async Task<CreateToolDefinitionResponse> CreateDraftToolAsync(
        HttpClient client,
        ManufacturingModelPackageFixture.PublishedPackageContext packageContext)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tools")
        {
            Content = JsonContent.Create(new CreateToolDefinitionRequest(
                "Draft Workflow Tool",
                "Draft tool for workflow readiness test.",
                $"draft-workflow-tool-{Guid.NewGuid():N}"[..24],
                "analysis",
                ToolRiskLevels.Low,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                true,
                ["governed_query.run"],
                """{"type":"object","required":["queryText"],"properties":{"queryText":{"type":"string"}}}""",
                """{"type":"object","required":["safeSummary"],"properties":{"safeSummary":{"type":"string"}}}""",
                "governed-query-v1",
                null,
                null,
                [packageContext.ModelPackage.Id],
                null,
                null,
                null,
                null,
                null,
                []))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateToolDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
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
}
