using ETOS.Backend.Identity;
using ETOS.Backend.WorkflowRuns;
using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class WorkflowOrchestrationCoordinatorTests
{
    [Fact]
    public void PickNextStep_RespectsDependsOnOrdering()
    {
        var steps = new List<WorkflowDefinitionPayloadParser.WorkflowStepDocument>
        {
            new() { StepKey = "b", DependsOnStepKeys = ["a"] },
            new() { StepKey = "a", DependsOnStepKeys = [] },
            new() { StepKey = "c", DependsOnStepKeys = ["b"] }
        };

        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var first = WorkflowOrchestrationCoordinator.PickNextStep(steps, completed);
        Assert.Equal("a", first!.StepKey);

        completed.Add("a");
        var second = WorkflowOrchestrationCoordinator.PickNextStep(steps, completed);
        Assert.Equal("b", second!.StepKey);
    }

    [Fact]
    public void ResolveFinalStatus_ReturnsPreviewSucceededForPreviewRuns()
    {
        var status = WorkflowOrchestrationCoordinator.ResolveFinalStatus(
            [new WorkflowStepExecutionResult("step-1", WorkflowRunStatuses.PreviewSucceeded, "{}", null, null, null, null, null)],
            isPreview: true,
            safeModeApplied: false,
            stopWorkflow: false,
            completedCount: 1,
            totalCount: 1);

        Assert.Equal(WorkflowRunStatuses.PreviewSucceeded, status);
    }

    [Fact]
    public void ResolveFinalStatus_ReturnsSafeModeBlockedWhenBlockedStepAppliedSafeMode()
    {
        var status = WorkflowOrchestrationCoordinator.ResolveFinalStatus(
            [new WorkflowStepExecutionResult("step-1", WorkflowRunStatuses.Blocked, "{}", null, null, null, null, null)],
            isPreview: false,
            safeModeApplied: true,
            stopWorkflow: true,
            completedCount: 1,
            totalCount: 2);

        Assert.Equal(WorkflowRunStatuses.SafeModeBlocked, status);
    }

    [Fact]
    public async Task RunAsync_StopsAfterBlockedStepWhenPartialCompletionDisabled()
    {
        var executor = new RecordingWorkflowStepExecutor([
            new WorkflowStepExecutionResult("step-1", WorkflowRunStatuses.Blocked, """{"step":1}""", null, null, null, null, null),
            new WorkflowStepExecutionResult("step-2", WorkflowRunStatuses.Succeeded, """{"step":2}""", null, null, null, null, null)
        ]);

        var coordinator = new WorkflowOrchestrationCoordinator(executor);
        var request = CreateRequest(
            allowPartialCompletion: false,
            steps:
            [
                new WorkflowStepDefinitionRequest("step-1", WorkflowStepTypes.BusinessPolicyCheck, WorkflowStepSafeModeBehaviors.Skip, null, null, null, null, null, null, null),
                new WorkflowStepDefinitionRequest("step-2", WorkflowStepTypes.BusinessPolicyCheck, WorkflowStepSafeModeBehaviors.Skip, ["step-1"], null, null, null, null, null, null)
            ]);

        var result = await coordinator.RunAsync(request, CancellationToken.None);

        Assert.Equal(WorkflowRunStatuses.Blocked, result.Status);
        Assert.False(result.PartialCompletion);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task RunAsync_SetsPartialCompletionWhenBlockedBeforeAllStepsComplete()
    {
        var executor = new RecordingWorkflowStepExecutor([
            new WorkflowStepExecutionResult("step-1", WorkflowRunStatuses.Blocked, """{"step":1}""", null, null, null, null, null),
            new WorkflowStepExecutionResult("step-2", WorkflowRunStatuses.Succeeded, """{"step":2}""", null, null, null, null, null)
        ]);

        var coordinator = new WorkflowOrchestrationCoordinator(executor);
        var request = CreateRequest(
            allowPartialCompletion: true,
            steps:
            [
                new WorkflowStepDefinitionRequest("step-1", WorkflowStepTypes.BusinessPolicyCheck, WorkflowStepSafeModeBehaviors.Skip, null, null, null, null, null, null, null),
                new WorkflowStepDefinitionRequest("step-2", WorkflowStepTypes.BusinessPolicyCheck, WorkflowStepSafeModeBehaviors.Skip, ["step-1"], null, null, null, null, null, null)
            ]);

        var result = await coordinator.RunAsync(request, CancellationToken.None);

        Assert.Equal(WorkflowRunStatuses.Blocked, result.Status);
        Assert.True(result.PartialCompletion);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task DaprAdapter_ThrowsWhenDaprHostDisabled()
    {
        var adapter = new DaprWorkflowRuntimeAdapter(
            Options.Create(new WorkflowRuntimeOptions { EnableDaprHost = false }),
            new ServiceCollection().BuildServiceProvider());

        var request = CreateRequest(false, []);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => adapter.StartManualRunAsync(request, CancellationToken.None));

        Assert.Contains("EnableDaprHost", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkflowRuntimeStartRequest CreateRequest(
        bool allowPartialCompletion,
        IReadOnlyCollection<WorkflowStepDefinitionRequest> steps)
    {
        var stepDocuments = steps.Select(step => new WorkflowDefinitionPayloadParser.WorkflowStepDocument
        {
            StepKey = step.StepKey,
            StepType = step.StepType,
            SafeModeOnBlock = step.SafeModeOnBlock,
            DependsOnStepKeys = step.DependsOnStepKeys?.ToList(),
            AgentVersionId = step.AgentVersionId,
            ToolDefinitionVersionId = step.ToolDefinitionVersionId,
            BusinessPolicyDefinitionVersionId = step.BusinessPolicyDefinitionVersionId,
            OptimizationModelVersionId = step.OptimizationModelVersionId,
            SourceStepKey = step.SourceStepKey,
            ReviewTaskTemplateVersionId = step.ReviewTaskTemplateVersionId
        }).ToList();

        var payload = new WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument
        {
            WorkflowKey = $"coord-{Guid.NewGuid():N}"[..20],
            DisplayName = "Coordinator Test Workflow",
            WorkflowScope = WorkflowScopes.Tenant,
            WorkflowDefinitionJson = WorkflowDefinitionPayloadParser.SerializeWorkflowDefinitionJson(stepDocuments),
            AllowPartialCompletion = allowPartialCompletion,
            DefaultStepSafeModeBehavior = WorkflowStepSafeModeBehaviors.Skip,
            TriggerConfig = new WorkflowDefinitionPayloadParser.TriggerConfigDocument
            {
                Manual = new WorkflowDefinitionPayloadParser.ManualTriggerDocument { Enabled = true }
            }
        };

        return new WorkflowRuntimeStartRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            payload,
            "{}",
            WorkflowExecutionMode.Preview,
            true,
            false,
            false,
            allowPartialCompletion);
    }

    private sealed class RecordingWorkflowStepExecutor(IReadOnlyList<WorkflowStepExecutionResult> scriptedResults)
        : IWorkflowStepExecutor
    {
        private int _index;

        public int CallCount { get; private set; }

        public Task<WorkflowStepExecutionResult> ExecuteAsync(
            WorkflowStepExecutionContext context,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var result = scriptedResults[Math.Min(_index, scriptedResults.Count - 1)];
            _index++;
            return Task.FromResult(result with { StepKey = context.Step.StepKey ?? result.StepKey });
        }
    }
}
