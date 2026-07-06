using System.Text.Json;
using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuns;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class WorkflowOrchestrationCoordinator(IWorkflowStepExecutor stepExecutor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowRuntimeStartResult> RunAsync(
        WorkflowRuntimeStartRequest request,
        CancellationToken cancellationToken)
    {
        var input = ToOrchestrationInput(request);
        var state = CreateInitialState(request.InputContextJson, request.SafeModeActive);

        while (state.CompletedStepKeys.Count < input.Steps.Count && !state.StopWorkflow)
        {
            var nextStep = PickNextStep(input.Steps, state.CompletedStepKeys);
            if (nextStep is null)
            {
                break;
            }

            state = await ExecuteStepAsync(input, state, nextStep, cancellationToken);
        }

        return ToRuntimeResult(input, state);
    }

    public async Task<WorkflowStepActivityOutput> RunSingleStepAsync(
        WorkflowStepActivityInput input,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteStepCoreAsync(
            input.WorkflowRunId,
            input.WorkflowVersionId,
            input.TenantId,
            input.UserId,
            input.Step,
            input.Payload,
            input.AccumulatedContextJson,
            input.Mode,
            input.IsPreview,
            input.IsDryRun,
            input.SafeModeActive,
            cancellationToken);

        return new WorkflowStepActivityOutput(result);
    }

    public static WorkflowOrchestrationState ApplyStepResult(
        WorkflowOrchestrationState state,
        WorkflowStepExecutionResult result,
        bool allowPartialCompletion)
    {
        state.StepResults.Add(result);
        state.CompletedStepKeys.Add(result.StepKey.Trim());

        if (!string.IsNullOrWhiteSpace(result.OutputContextJson))
        {
            state.AccumulatedContextJson = result.OutputContextJson;
        }

        if (result.RecommendationArtifactId is Guid recommendationArtifactId)
        {
            state.RecommendationArtifactIds.Add(recommendationArtifactId);
        }

        if (result.ReviewTaskArtifactId is Guid reviewTaskArtifactId)
        {
            state.ReviewTaskArtifactIds.Add(reviewTaskArtifactId);
        }

        if (result.SafeModeEvent is not null)
        {
            state.SafeModeEvents.Add(result.SafeModeEvent);
            state.SafeModeApplied = true;
        }

        if (result.Status.Equals(WorkflowRunStatuses.Blocked, StringComparison.OrdinalIgnoreCase))
        {
            state.StopWorkflow = true;
        }

        return state;
    }

    public static string ResolveFinalStatus(
        IReadOnlyCollection<WorkflowStepExecutionResult> stepResults,
        bool isPreview,
        bool safeModeApplied,
        bool stopWorkflow,
        int completedCount,
        int totalCount)
    {
        if (stepResults.Any(item => item.Status.Equals(WorkflowRunStatuses.Blocked, StringComparison.OrdinalIgnoreCase)))
        {
            return safeModeApplied ? WorkflowRunStatuses.SafeModeBlocked : WorkflowRunStatuses.Blocked;
        }

        if (safeModeApplied)
        {
            return WorkflowRunStatuses.SafeModeCompleted;
        }

        if (isPreview)
        {
            return WorkflowRunStatuses.PreviewSucceeded;
        }

        if (completedCount < totalCount)
        {
            return WorkflowRunStatuses.Failed;
        }

        return WorkflowRunStatuses.Succeeded;
    }

    public static WorkflowOrchestrationInput ToOrchestrationInput(WorkflowRuntimeStartRequest request)
    {
        var steps = WorkflowDefinitionPayloadParser.DeserializeWorkflowDefinitionJson(
            request.Payload.WorkflowDefinitionJson ?? "[]");

        return new WorkflowOrchestrationInput(
            request.WorkflowRunId,
            request.WorkflowVersionId,
            request.WorkflowArtifactId,
            request.TenantId,
            request.UserId,
            request.Payload,
            request.InputContextJson,
            request.Mode,
            request.IsPreview,
            request.IsDryRun,
            request.SafeModeActive,
            request.AllowPartialCompletion,
            steps);
    }

    public static WorkflowOrchestrationState CreateInitialState(string inputContextJson, bool safeModeActive)
        => new()
        {
            AccumulatedContextJson = inputContextJson,
            SafeModeApplied = safeModeActive
        };

    public static WorkflowDefinitionPayloadParser.WorkflowStepDocument? PickNextStep(
        IReadOnlyList<WorkflowDefinitionPayloadParser.WorkflowStepDocument> steps,
        ISet<string> completedStepKeys)
        => steps.FirstOrDefault(step =>
            !completedStepKeys.Contains(step.StepKey!.Trim())
            && (step.DependsOnStepKeys ?? []).All(dependency => completedStepKeys.Contains(dependency)));

    public WorkflowRuntimeStartResult ToRuntimeResult(
        WorkflowOrchestrationInput input,
        WorkflowOrchestrationState state)
    {
        var output = ToOrchestrationOutput(input, state);
        return new WorkflowRuntimeStartResult(
            output.Status,
            output.OutputContextJson,
            output.StepResultsJson,
            output.RecommendationArtifactIds,
            output.ReviewTaskArtifactIds,
            output.SafeModeEvents.Select(ToEntity).ToArray(),
            output.PartialCompletion,
            output.SafeModeApplied);
    }

    public static WorkflowOrchestrationOutput ToOrchestrationOutput(
        WorkflowOrchestrationInput input,
        WorkflowOrchestrationState state)
    {
        var finalStatus = ResolveFinalStatus(
            state.StepResults,
            input.IsPreview,
            state.SafeModeApplied,
            state.StopWorkflow,
            state.CompletedStepKeys.Count,
            input.Steps.Count);

        var stepResultsJson = SerializeStepResults(state.StepResults);
        var partialCompletion = input.AllowPartialCompletion
            && state.StopWorkflow
            && state.CompletedStepKeys.Count < input.Steps.Count;

        return new WorkflowOrchestrationOutput(
            finalStatus,
            state.AccumulatedContextJson,
            stepResultsJson,
            state.RecommendationArtifactIds,
            state.ReviewTaskArtifactIds,
            state.SafeModeEvents.Select(ToSnapshot).ToArray(),
            partialCompletion,
            state.SafeModeApplied);
    }

    public static WorkflowRuntimeStartResult ToRuntimeResult(
        WorkflowOrchestrationInput input,
        WorkflowOrchestrationOutput output)
        => new(
            output.Status,
            output.OutputContextJson,
            output.StepResultsJson,
            output.RecommendationArtifactIds,
            output.ReviewTaskArtifactIds,
            output.SafeModeEvents.Select(snapshot => ToEntity(snapshot, input.TenantId, input.WorkflowRunId)).ToArray(),
            output.PartialCompletion,
            output.SafeModeApplied);

    private async Task<WorkflowOrchestrationState> ExecuteStepAsync(
        WorkflowOrchestrationInput input,
        WorkflowOrchestrationState state,
        WorkflowDefinitionPayloadParser.WorkflowStepDocument step,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteStepCoreAsync(
            input.WorkflowRunId,
            input.WorkflowVersionId,
            input.TenantId,
            input.UserId,
            step,
            input.Payload,
            state.AccumulatedContextJson,
            input.Mode,
            input.IsPreview,
            input.IsDryRun,
            input.SafeModeActive,
            cancellationToken);

        ApplyStepResult(state, result, input.AllowPartialCompletion);

        if (state.StopWorkflow && !input.AllowPartialCompletion)
        {
            return state;
        }

        return state;
    }

    private Task<WorkflowStepExecutionResult> ExecuteStepCoreAsync(
        Guid workflowRunId,
        Guid workflowVersionId,
        Guid tenantId,
        Guid userId,
        WorkflowDefinitionPayloadParser.WorkflowStepDocument step,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument payload,
        string accumulatedContextJson,
        WorkflowExecutionMode mode,
        bool isPreview,
        bool isDryRun,
        bool safeModeActive,
        CancellationToken cancellationToken)
    {
        var stepContext = new WorkflowStepExecutionContext(
            workflowRunId,
            workflowVersionId,
            tenantId,
            userId,
            step,
            payload,
            accumulatedContextJson,
            mode,
            isPreview,
            isDryRun,
            safeModeActive,
            safeModeActive);

        return stepExecutor.ExecuteAsync(stepContext, cancellationToken);
    }

    private static string SerializeStepResults(IReadOnlyCollection<WorkflowStepExecutionResult> stepResults)
        => JsonSerializer.Serialize(stepResults.Select(item => new
        {
            item.StepKey,
            item.Status,
            item.AgentRunId,
            item.ToolRunId,
            item.RecommendationArtifactId,
            item.ReviewTaskArtifactId
        }), JsonOptions);

    private static SafeModeEventSnapshot ToSnapshot(SafeModeEvent safeModeEvent)
        => new(
            safeModeEvent.StepKey,
            safeModeEvent.EventKind,
            safeModeEvent.Reason,
            safeModeEvent.PolicyRuleKey,
            safeModeEvent.BlockedAction,
            safeModeEvent.AgentRunId,
            safeModeEvent.ToolRunId,
            safeModeEvent.ReviewTaskArtifactId);

    private static SafeModeEvent ToEntity(SafeModeEventSnapshot snapshot)
        => new()
        {
            StepKey = snapshot.StepKey,
            EventKind = snapshot.EventKind,
            Reason = snapshot.Reason,
            PolicyRuleKey = snapshot.PolicyRuleKey,
            BlockedAction = snapshot.BlockedAction,
            AgentRunId = snapshot.AgentRunId,
            ToolRunId = snapshot.ToolRunId,
            ReviewTaskArtifactId = snapshot.ReviewTaskArtifactId
        };

    private static SafeModeEvent ToEntity(SafeModeEventSnapshot snapshot, Guid tenantId, Guid workflowRunId)
    {
        var entity = ToEntity(snapshot);
        entity.TenantId = tenantId;
        entity.WorkflowRunId = workflowRunId;
        return entity;
    }
}
