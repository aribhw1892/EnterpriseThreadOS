using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Governance;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.Recommendations;
using ETOS.Backend.Tests.Fixtures;
using ETOS.Backend.WorkflowRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class MvpDemonstrationFlowTests
{
    [Fact]
    public async Task HappyPath_CoversPrdSteps1Through20()
    {
        var graphMemory = new ImportFlowTestSupport.RecordingGraphMemoryService();
        await using var application = MvpDemonstrationFlowSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();

        var result = await MvpDemonstrationFlowSupport.RunHappyPathAsync(client, application, graphMemory);

        Assert.NotEqual(Guid.Empty, result.TenantId);
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.NotEqual(Guid.Empty, result.TrustedGraphSnapshotId);
        Assert.NotEqual(Guid.Empty, result.BomComparisonRunId);
        Assert.NotEqual(Guid.Empty, result.RecommendationArtifactId);
        Assert.NotEqual(Guid.Empty, result.ReviewTaskArtifactId);
        Assert.NotEqual(Guid.Empty, result.DecisionArtifactId);
        Assert.NotEqual(Guid.Empty, result.OutcomeRecordId);
        Assert.NotEqual(Guid.Empty, result.CustomAgentRunId);
        Assert.NotEqual(Guid.Empty, result.WorkflowRunId);
        Assert.NotNull(result.LearningSignalArtifactId);
        Assert.NotEmpty(result.AuditRecordIds);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();

        var workflowRun = await dbContext.WorkflowRuns.SingleAsync(item => item.Id == result.WorkflowRunId);
        Assert.True(
            workflowRun.Status is WorkflowRunStatuses.Succeeded or WorkflowRunStatuses.SafeModeCompleted,
            $"Unexpected workflow status '{workflowRun.Status}'.");

        var decisionExists = await dbContext.Artifacts.AnyAsync(
            artifact => artifact.Id == result.DecisionArtifactId
                && artifact.NormalizedArtifactType == DecisionArtifactTypes.Decision.ToUpperInvariant());
        Assert.True(decisionExists);

        var learningSignalExists = await dbContext.Artifacts.AnyAsync(
            artifact => artifact.Id == result.LearningSignalArtifactId
                && artifact.NormalizedArtifactType == LearningArtifactTypes.LearningSignal.ToUpperInvariant());
        Assert.True(learningSignalExists);

        var writeConnectorRuns = await MvpDemonstrationFlowSupport.CountSuccessfulWriteConnectorRunsAsync(dbContext, result.TenantId);
        Assert.Equal(0, writeConnectorRuns);

        var auditRecords = await dbContext.AuditRecords.Where(record => record.TenantId == result.TenantId).ToListAsync();
        Assert.Contains(auditRecords, record => record.Action.Contains("promote", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(auditRecords, record => record.Action.Contains("agent", StringComparison.OrdinalIgnoreCase) || record.Action.Contains("workflow", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.AuditRecordIds.Count >= 3);
    }

    [Fact]
    public async Task DeniedPath_RestrictedContextFailsClosed()
    {
        var graphMemory = new ImportFlowTestSupport.RecordingGraphMemoryService();
        await using var application = MvpDemonstrationFlowSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();

        await MvpDemonstrationFlowSupport.RunDeniedPathAsync(client, application, graphMemory);
    }
}
