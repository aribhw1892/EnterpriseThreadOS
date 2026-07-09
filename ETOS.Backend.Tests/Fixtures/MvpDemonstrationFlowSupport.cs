using System.Net.Http.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.WorkflowRuns;
using ETOS.Backend.WorkflowRuntime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests.Fixtures;

public static class MvpDemonstrationFlowSupport
{
    public sealed record MvpDemonstrationResult(
        Guid TenantId,
        Guid UserId,
        Guid TrustedGraphSnapshotId,
        Guid BomComparisonRunId,
        Guid RecommendationArtifactId,
        Guid ReviewTaskArtifactId,
        Guid DecisionArtifactId,
        Guid OutcomeRecordId,
        Guid? LearningSignalArtifactId,
        Guid CustomAgentRunId,
        Guid WorkflowRunId,
        IReadOnlyList<Guid> AuditRecordIds);

    public static WebApplicationFactory<Program> CreateApplication(
        ImportFlowTestSupport.RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));
        var storageRoot = Path.Combine(Path.GetTempPath(), "etos-mvp-demo-tests", Guid.NewGuid().ToString("N"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ImportFileStorage:RootPath"] = storageRoot,
                        ["ReferencePackages:RootPath"] = packagesRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false",
                        ["ImportMappingSuggestions:DefaultProviderKey"] = "rule-based-v1",
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

                    services.RemoveAll<IGraphMemoryService>();
                    services.AddSingleton<IGraphMemoryService>(graphMemory ?? new ImportFlowTestSupport.RecordingGraphMemoryService());
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

    public static async Task<MvpDemonstrationResult> RunHappyPathAsync(
        HttpClient client,
        WebApplicationFactory<Program> application,
        ImportFlowTestSupport.RecordingGraphMemoryService graphMemory)
    {
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client, "mvp-demo", "mvp-admin@example.test");
        var auditRecordIds = new List<Guid>();

        var (cadBatch, _) = await ImportFlowTestSupport.PrepareStagedImportAsync(
            client,
            context,
            "demo-cad-pdm",
            "flat-part-import.csv");
        var (erpIdentityBatch, _) = await ImportFlowTestSupport.PrepareStagedImportAsync(
            client,
            context,
            "demo-erp",
            "flat-part-import.csv");

        var generated = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, erpIdentityBatch.Id);
        foreach (var candidate in generated.Candidates.Where(item => item.State != IdentityCandidateState.Approved))
        {
            await ImportFlowTestSupport.ApproveIdentityCandidateAsync(client, context, candidate.Id);
        }

        var promotion = await ImportFlowTestSupport.PromoteBatchAsync(client, context, cadBatch.Id);
        if (promotion.AuditRecordId.HasValue)
        {
            auditRecordIds.Add(promotion.AuditRecordId.Value);
        }

        var snapshot = await ImportFlowTestSupport.CaptureSnapshotAsync(client, context, GraphSpace.Trusted);
        var trustedNodeId = graphMemory.Nodes
            .First(node => node.TenantId == context.TenantId && node.GraphSpace == GraphSpace.Trusted)
            .NodeId;

        var chatSession = await GovernanceFlowTestSupport.CreateGovernedChatSessionAsync(client, context, trustedNodeId);
        var chatTurn = await GovernanceFlowTestSupport.AskGovernedChatAsync(
            client,
            context,
            chatSession.Id,
            "Summarize BOM impact for promoted parts.",
            "bom-impact-context",
            trustedNodeId);
        auditRecordIds.Add(await ResolveAiTraceAuditIdAsync(application, chatTurn.AiTraceRecordId));

        var draftTurn = await GovernanceFlowTestSupport.AskGovernedChatAsync(
            client,
            context,
            chatSession.Id,
            "Draft an MVP dashboard for BOM impact review.",
            "bom-impact-context",
            trustedNodeId,
            ChatDraftArtifactKind.Dashboard,
            "default-context");

        var (bomBatch, _) = await ImportFlowTestSupport.PrepareStagedImportAsync(
            client,
            context,
            "demo-erp",
            "bom-comparison.csv");
        var bomComparison = await ImportFlowTestSupport.CreateBomComparisonAsync(client, context, bomBatch.Id);
        if (bomComparison.AuditRecordId.HasValue)
        {
            auditRecordIds.Add(bomComparison.AuditRecordId.Value);
        }

        var recommendation = await GovernanceFlowTestSupport.CreateRecommendationFromBomComparisonAsync(client, context, bomComparison.Id);
        var recommendationDetail = await GovernanceFlowTestSupport.GetRecommendationAsync(
            client,
            context,
            recommendation.ArtifactId,
            recommendation.VersionId);
        var actionId = recommendationDetail.SuggestedActions.First().ActionId;

        var reviewTask = await GovernanceFlowTestSupport.CreateReviewTaskFromRecommendationActionAsync(
            client,
            context,
            recommendation.ArtifactId,
            recommendation.VersionId,
            actionId);
        var completedReview = await GovernanceFlowTestSupport.CompleteReviewTaskAsync(
            client,
            context,
            reviewTask.ArtifactId,
            reviewTask.VersionId);
        Assert.NotNull(completedReview.DecisionArtifactId);
        Assert.NotNull(completedReview.DecisionVersionId);

        var outcome = await GovernanceFlowTestSupport.RecordDecisionOutcomeAsync(
            client,
            context,
            completedReview.DecisionArtifactId.Value,
            completedReview.DecisionVersionId.Value,
            recommendation.ArtifactId);

        var learningSignalArtifactId = await EnsureLearningSignalRollupAsync(
            application,
            context,
            completedReview.DecisionArtifactId.Value,
            completedReview.DecisionVersionId.Value);

        var (agent, _) = await AgentExecutionTestSupport.PreparePublishedManufacturingAgentAsync(
            client,
            application,
            context.TenantId,
            context.UserId,
            "mvp-manufacturing-investigator");
        var agentExecution = await AgentExecutionTestSupport.ExecuteAgentAsync(
            client,
            context.TenantId,
            context.UserId,
            agent.ArtifactId,
            agent.VersionId,
            "Investigate manufacturing BOM impact context.",
            trustedNodeId);
        auditRecordIds.Add(await ResolveAgentRunAuditIdAsync(application, agentExecution.AgentRunId));

        var workflow = await WorkflowExecutionTestSupport.ResolvePublishedWorkflowAsync(application, context.TenantId, "bom-impact-review");
        var workflowExecution = await WorkflowExecutionTestSupport.ExecuteWorkflowAsync(
            client,
            context.TenantId,
            context.UserId,
            workflow.ArtifactId,
            workflow.VersionId,
            $$"""{"intentKey":"bom-impact-context","queryText":"Investigate BOM impact for assembly A-100.","startGraphNodeId":"{{trustedNodeId}}"}""");
        auditRecordIds.Add(await ResolveWorkflowRunAuditIdAsync(application, workflowExecution.WorkflowRunId));

        if (draftTurn.DraftArtifact is not null)
        {
            _ = draftTurn.DraftArtifact.ArtifactId;
        }

        return new MvpDemonstrationResult(
            context.TenantId,
            context.UserId,
            snapshot.SnapshotId,
            bomComparison.Id,
            recommendation.ArtifactId,
            reviewTask.ArtifactId,
            completedReview.DecisionArtifactId.Value,
            outcome.OutcomeCheckRunId,
            learningSignalArtifactId,
            agentExecution.AgentRunId,
            workflowExecution.WorkflowRunId,
            auditRecordIds);
    }

    public static async Task RunDeniedPathAsync(
        HttpClient client,
        WebApplicationFactory<Program> application,
        ImportFlowTestSupport.RecordingGraphMemoryService graphMemory)
    {
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client, "mvp-denied", "mvp-denied-admin@example.test");
        var (cadBatch, _) = await ImportFlowTestSupport.PrepareStagedImportAsync(
            client,
            context,
            "demo-cad-pdm",
            "flat-part-import.csv");
        await ImportFlowTestSupport.PromoteBatchAsync(client, context, cadBatch.Id);
        var trustedNodeId = graphMemory.Nodes
            .FirstOrDefault(node => node.TenantId == context.TenantId && node.GraphSpace == GraphSpace.Trusted)
            ?.NodeId ?? Guid.NewGuid();

        await GovernanceFlowTestSupport.CreatePublishedDenyPolicyAsync(client, context);
        var chatRunner = await GovernanceFlowTestSupport.CreateChatRunnerContextAsync(client, context);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var recommendationCountBefore = await dbContext.Artifacts.CountAsync(
            artifact => artifact.TenantId == context.TenantId
                && artifact.NormalizedArtifactType == RecommendationArtifactTypes.Recommendation.ToUpperInvariant());
        var decisionCountBefore = await dbContext.Artifacts.CountAsync(
            artifact => artifact.TenantId == context.TenantId
                && artifact.NormalizedArtifactType == DecisionArtifactTypes.Decision.ToUpperInvariant());

        var evaluation = await GovernanceFlowTestSupport.EvaluateRestrictedContextAsync(client, chatRunner);
        Assert.Empty(evaluation.AllowedContext);
        Assert.NotEmpty(evaluation.DeniedSummaries);

        var audit = await dbContext.AuditRecords
            .Where(record => record.TenantId == context.TenantId
                && record.Reason == "policy_context_denied")
            .OrderByDescending(record => record.CreatedAt)
            .FirstAsync();
        Assert.Equal(Governance.AuditResult.Denied, audit.Result);
        Assert.Equal("policy_context_denied", audit.Reason);

        var session = await GovernanceFlowTestSupport.CreateGovernedChatSessionAsync(client, chatRunner, trustedNodeId);
        var (draftStatusCode, _) = await GovernanceFlowTestSupport.PostGovernedChatTurnAsync(
            client,
            chatRunner,
            session.Id,
            "Draft a restricted report.",
            "bom-impact-context",
            trustedNodeId,
            ChatDraftArtifactKind.Report,
            "default-context");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, draftStatusCode);

        var recommendationCountAfter = await dbContext.Artifacts.CountAsync(
            artifact => artifact.TenantId == context.TenantId
                && artifact.NormalizedArtifactType == RecommendationArtifactTypes.Recommendation.ToUpperInvariant());
        var decisionCountAfter = await dbContext.Artifacts.CountAsync(
            artifact => artifact.TenantId == context.TenantId
                && artifact.NormalizedArtifactType == DecisionArtifactTypes.Decision.ToUpperInvariant());
        Assert.Equal(recommendationCountBefore, recommendationCountAfter);
        Assert.Equal(decisionCountBefore, decisionCountAfter);
    }

    public static async Task<int> CountSuccessfulWriteConnectorRunsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId)
    {
        var toolVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(version => version.Artifact)
            .Where(version => version.TenantId == tenantId
                && version.Artifact!.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition)
            .ToListAsync();

        var writeToolVersionIds = toolVersions
            .Where(version =>
            {
                var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
                return string.Equals(document.InternalHandlerKey, ToolInternalHandlerKeys.DisabledWriteConnector, StringComparison.OrdinalIgnoreCase)
                    || document.WritesExternalSystem;
            })
            .Select(version => version.Id)
            .ToHashSet();

        return await dbContext.ToolRuns.CountAsync(run =>
            run.TenantId == tenantId
            && run.Status == ToolRunStatuses.Succeeded
            && writeToolVersionIds.Contains(run.ToolDefinitionVersionId));
    }

    private static async Task<Guid?> EnsureLearningSignalRollupAsync(
        WebApplicationFactory<Program> application,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid decisionArtifactId,
        Guid decisionVersionId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var decisionVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleAsync(version => version.Id == decisionVersionId);
        var payload = DecisionPayloadParser.Deserialize(decisionVersion.PayloadJson ?? "{}");
        var patternKey = LearningEvidenceEmitter.BuildPatternKey(payload);

        for (var i = 0; i < 2; i++)
        {
            dbContext.DecisionLearningEvidence.Add(new DecisionLearningEvidence
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                PatternKey = patternKey,
                SourceType = "manual",
                OutcomeKey = "accept",
                EvidenceSummary = $"Supplemental MVP evidence {i}.",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        var rollup = new LearningSignalRollupService(
            dbContext,
            Options.Create(new LearningSignalRollupOptions
            {
                MinOccurrences = 3,
                WindowDays = 30
            }));
        await rollup.EvaluateAsync(context.TenantId, context.UserId, payload, CancellationToken.None);

        var signal = await dbContext.Artifacts
            .Where(artifact => artifact.TenantId == context.TenantId
                && artifact.NormalizedArtifactType == LearningArtifactTypes.LearningSignal.ToUpperInvariant())
            .Select(artifact => artifact.Id)
            .FirstOrDefaultAsync();

        return signal == Guid.Empty ? null : signal;
    }

    private static async Task<Guid> ResolveAiTraceAuditIdAsync(WebApplicationFactory<Program> application, Guid aiTraceRecordId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var trace = await dbContext.AiTraceRecords.AsNoTracking().SingleAsync(record => record.Id == aiTraceRecordId);
        return trace.AuditRecordId ?? aiTraceRecordId;
    }

    private static async Task<Guid> ResolveAgentRunAuditIdAsync(WebApplicationFactory<Program> application, Guid agentRunId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var run = await dbContext.AgentRuns.AsNoTracking().SingleAsync(record => record.Id == agentRunId);
        return run.AuditRecordId ?? agentRunId;
    }

    private static async Task<Guid> ResolveWorkflowRunAuditIdAsync(WebApplicationFactory<Program> application, Guid workflowRunId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var run = await dbContext.WorkflowRuns.AsNoTracking().SingleAsync(record => record.Id == workflowRunId);
        return run.AuditRecordId ?? workflowRunId;
    }
}
