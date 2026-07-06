using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.WorkflowRuns;
using ETOS.Backend.WorkflowRuntime;
using ETOS.Backend.Tests.Fixtures;

namespace ETOS.Backend.Tests;

public sealed class WorkflowRunTests
{
    [Fact]
    public async Task ExecutePublishedWorkflowCreatesListableRun()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = WorkflowExecutionTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var workflow = await WorkflowExecutionTestSupport.ResolvePublishedWorkflowAsync(
            application,
            packageContext.TenantId,
            "bom-impact-review");

        var execution = await WorkflowExecutionTestSupport.ExecuteWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            workflow.ArtifactId,
            workflow.VersionId);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/workflow-runs");
        AgentExecutionTestSupport.AddTenantHeaders(listRequest, packageContext.TenantId, packageContext.UserId);
        var listResponse = await client.SendAsync(listRequest);
        var runs = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<WorkflowRunSummaryResponse>>();

        Assert.True(listResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(runs);
        Assert.Contains(runs, item => item.Id == execution.WorkflowRunId);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/workflow-runs/{execution.WorkflowRunId}");
        AgentExecutionTestSupport.AddTenantHeaders(getRequest, packageContext.TenantId, packageContext.UserId);
        var getResponse = await client.SendAsync(getRequest);
        var detail = await getResponse.Content.ReadFromJsonAsync<WorkflowRunDetailResponse>();

        Assert.True(getResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(detail);
        Assert.Equal(execution.WorkflowRunId, detail.Id);
        Assert.Equal(workflow.VersionId, detail.WorkflowVersionId);
        Assert.False(detail.IsPreview);
    }

    [Fact]
    public async Task CrossTenantGetDenied()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = WorkflowExecutionTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var ownerContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-wf-a", "owner@example.test");
        var workflow = await WorkflowExecutionTestSupport.ResolvePublishedWorkflowAsync(
            application,
            ownerContext.TenantId,
            "bom-impact-review");

        var execution = await WorkflowExecutionTestSupport.ExecuteWorkflowAsync(
            client,
            ownerContext.TenantId,
            ownerContext.UserId,
            workflow.ArtifactId,
            workflow.VersionId);

        var otherUserId = Guid.NewGuid();
        await AgentExecutionTestSupport.CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await AgentExecutionTestSupport.CreateTenantAsync(client, otherUserId, "tenant-wf-b");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/workflow-runs/{execution.WorkflowRunId}");
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
