using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class AgentExecutionE2ETests
{
    [Fact]
    public async Task ManufacturingInvestigatorExecuteLinksToolRunsTraceAndRecommendation()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = AgentExecutionTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, templateVersionId) = await AgentExecutionTestSupport.PreparePublishedManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "e2e-manufacturing-investigator");

        var execution = await AgentExecutionTestSupport.ExecuteAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId,
            "Investigate manufacturing BOM impact context.");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();

        var agentRun = await dbContext.AgentRuns.SingleAsync(item => item.Id == execution.AgentRunId);
        Assert.Equal(AgentRunStatuses.Succeeded, agentRun.Status);
        Assert.NotNull(agentRun.RecommendationArtifactId);
        Assert.NotNull(agentRun.AiTraceRecordId);
        Assert.NotNull(agentRun.RetrievalRunId);

        var toolRuns = await dbContext.ToolRuns
            .Where(item => item.ParentAgentRunId == execution.AgentRunId)
            .ToListAsync();
        Assert.NotEmpty(toolRuns);
        Assert.All(toolRuns, run => Assert.Equal(execution.AgentRunId, run.ParentAgentRunId));

        var trace = await dbContext.AiTraceRecords
            .Include(item => item.ArtifactLinks)
            .SingleAsync(item => item.Id == execution.AiTraceRecordId);
        Assert.Equal(AiTraceKind.AgentRun, trace.TraceKind);
        Assert.Equal(execution.AgentRunId, trace.AgentRunId);
        Assert.Contains(trace.ArtifactLinks, link => link.LinkKind == AiTraceArtifactLinkKind.AgentRun);
        Assert.Contains(trace.ArtifactLinks, link => link.LinkKind == AiTraceArtifactLinkKind.ToolRun);

        var recommendation = await dbContext.Artifacts.SingleAsync(item => item.Id == execution.RecommendationArtifactId);
        Assert.Equal(RecommendationArtifactTypes.Recommendation, recommendation.ArtifactType);

        using var detailRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agents/{agent.ArtifactId}/versions/{agent.VersionId}");
        AgentExecutionTestSupport.AddTenantHeaders(detailRequest, packageContext.TenantId, packageContext.UserId);
        var detailResponse = await client.SendAsync(detailRequest);
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        Assert.True(detailResponse.StatusCode == System.Net.HttpStatusCode.OK, detailBody);
        Assert.Contains(templateVersionId.ToString(), detailBody, StringComparison.OrdinalIgnoreCase);
    }
}
