using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Agents;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class AgentRunTests
{
    [Fact]
    public async Task PreviewCompletesWithoutRecommendation()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = AgentExecutionTestSupport.CreateApplication(graphMemory: graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "preview-agent");

        var execution = await AgentExecutionTestSupport.PreviewAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);

        Assert.Equal(AgentRunStatuses.PreviewSucceeded, execution.Status);
        Assert.True(execution.IsPreview);
        Assert.Null(execution.RecommendationArtifactId);
        Assert.NotNull(execution.AiTraceRecordId);
    }

    [Fact]
    public async Task ExecuteCreatesRecommendationAndChildToolRuns()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = AgentExecutionTestSupport.CreateApplication(graphMemory: graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PreparePublishedManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "execute-agent");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var decisionCountBefore = await dbContext.Artifacts.CountAsync(item =>
            item.TenantId == packageContext.TenantId
            && (item.NormalizedArtifactType == "DECISION" || item.NormalizedArtifactType == "DECISION-ARTIFACT"));

        var execution = await AgentExecutionTestSupport.ExecuteAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);

        Assert.Equal(AgentRunStatuses.Succeeded, execution.Status);
        Assert.False(execution.IsPreview);
        Assert.NotNull(execution.RecommendationArtifactId);
        Assert.NotEmpty(execution.ToolRunIds);

        var toolRuns = await dbContext.ToolRuns
            .Where(item => execution.ToolRunIds.Contains(item.Id))
            .ToListAsync();
        Assert.All(toolRuns, run => Assert.Equal(execution.AgentRunId, run.ParentAgentRunId));

        var decisionCountAfter = await dbContext.Artifacts.CountAsync(item =>
            item.TenantId == packageContext.TenantId
            && (item.NormalizedArtifactType == "DECISION" || item.NormalizedArtifactType == "DECISION-ARTIFACT"));
        Assert.Equal(decisionCountBefore, decisionCountAfter);
    }

    [Fact]
    public async Task SafeModeBlocksExecuteWithoutRecommendation()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "safe-mode-agent");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var version = await dbContext.ArtifactVersions.SingleAsync(item => item.Id == agent.VersionId);
        var document = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        document.SafeModeEnabled = true;
        document.BlockedModeMessage = "Safe mode is active for this agent.";
        version.PayloadJson = AgentDefinitionPayloadParser.Serialize(document);
        await dbContext.SaveChangesAsync();

        await AgentExecutionTestSupport.MarkAgentReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);
        await AgentExecutionTestSupport.PublishAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);

        using var executeRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{agent.ArtifactId}/versions/{agent.VersionId}/execute")
        {
            Content = JsonContent.Create(new AgentExecutionRequest(null, "Blocked by safe mode.", Guid.NewGuid()))
        };
        AgentExecutionTestSupport.AddTenantHeaders(executeRequest, packageContext.TenantId, packageContext.UserId);

        var executeResponse = await client.SendAsync(executeRequest);
        var execution = await executeResponse.Content.ReadFromJsonAsync<AgentExecutionResponse>();

        Assert.True(executeResponse.StatusCode == HttpStatusCode.OK, await executeResponse.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        Assert.Equal(AgentRunStatuses.SafeModeBlocked, execution.Status);
        Assert.Null(execution.RecommendationArtifactId);
        Assert.Empty(execution.ToolRunIds);
        Assert.Contains("Safe mode", execution.ValidationNotes.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestRunLinksToolRunsToParentAgentRun()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = AgentExecutionTestSupport.CreateApplication(graphMemory: graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "test-run-agent");

        var execution = await AgentExecutionTestSupport.TestRunAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);

        Assert.True(execution.IsDryRun);
        Assert.NotEmpty(execution.ToolRunIds);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var toolRuns = await dbContext.ToolRuns
            .Where(item => execution.ToolRunIds.Contains(item.Id))
            .ToListAsync();

        Assert.All(toolRuns, run =>
        {
            Assert.Equal(execution.AgentRunId, run.ParentAgentRunId);
            Assert.True(run.IsDryRun);
        });
    }
}
