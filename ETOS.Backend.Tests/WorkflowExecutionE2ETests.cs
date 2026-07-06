using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Decisions;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.WorkflowRuns;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class WorkflowExecutionE2ETests
{
    [Fact]
    public async Task BomImpactReviewExecuteLinksChildRunsTraceAndOutputsWithoutDecisions()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = WorkflowExecutionTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var workflow = await WorkflowExecutionTestSupport.ResolvePublishedWorkflowAsync(
            application,
            packageContext.TenantId,
            "bom-impact-review");

        var startGraphNodeId = Guid.NewGuid();
        var execution = await WorkflowExecutionTestSupport.ExecuteWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            workflow.ArtifactId,
            workflow.VersionId,
            $$"""{"intentKey":"bom-impact-context","queryText":"Investigate BOM impact for assembly A-100.","startGraphNodeId":"{{startGraphNodeId}}"}""");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();

        var workflowRun = await dbContext.WorkflowRuns.SingleAsync(item => item.Id == execution.WorkflowRunId);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/workflow-runs/{execution.WorkflowRunId}");
        AgentExecutionTestSupport.AddTenantHeaders(getRequest, packageContext.TenantId, packageContext.UserId);
        var getResponse = await client.SendAsync(getRequest);
        var detail = await getResponse.Content.ReadFromJsonAsync<WorkflowRunDetailResponse>();
        Assert.True(getResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(detail);

        var agentRuns = await dbContext.AgentRuns
            .Where(item => item.ParentWorkflowRunId == execution.WorkflowRunId)
            .ToListAsync();

        var toolRuns = await dbContext.ToolRuns
            .Where(item => item.ParentWorkflowRunId == execution.WorkflowRunId)
            .ToListAsync();

        Assert.True(
            agentRuns.Count > 0 || toolRuns.Count > 0 || detail.ChildAgentRunIds.Count > 0 || detail.ChildToolRunIds.Count > 0,
            $"Expected child runs for workflow '{workflowRun.Status}' with steps: {workflowRun.StepResultsJson}");
        Assert.False(workflowRun.IsPreview);
        Assert.NotNull(workflowRun.AiTraceRecordId);
        Assert.NotNull(workflowRun.AuditRecordId);
        Assert.All(agentRuns, run => Assert.Equal(execution.WorkflowRunId, run.ParentWorkflowRunId));
        Assert.All(toolRuns, run => Assert.Equal(execution.WorkflowRunId, run.ParentWorkflowRunId));

        var trace = await dbContext.AiTraceRecords
            .Include(item => item.ArtifactLinks)
            .SingleAsync(item => item.Id == execution.AiTraceRecordId);
        Assert.Equal(AiTraceKind.WorkflowRun, trace.TraceKind);
        Assert.Equal(execution.WorkflowRunId, trace.WorkflowRunId);
        Assert.Contains(trace.ArtifactLinks, link => link.LinkKind == AiTraceArtifactLinkKind.WorkflowRun);

        var decisions = await dbContext.Artifacts
            .Where(item => item.TenantId == packageContext.TenantId
                && item.NormalizedArtifactType == DecisionArtifactTypes.Decision.ToUpperInvariant())
            .ToListAsync();
        Assert.Empty(decisions);

        if (workflowRun.Status is WorkflowRunStatuses.Succeeded or WorkflowRunStatuses.SafeModeCompleted)
        {
            Assert.False(string.IsNullOrWhiteSpace(workflowRun.RecommendationArtifactIdsJson));
            Assert.False(string.IsNullOrWhiteSpace(workflowRun.ReviewTaskArtifactIdsJson));
        }

        Assert.True(
            workflowRun.Status is WorkflowRunStatuses.Succeeded
                or WorkflowRunStatuses.SafeModeCompleted,
            $"Unexpected workflow status '{workflowRun.Status}' with steps: {workflowRun.StepResultsJson}");
    }
}
