using Dapr.Workflow;

namespace ETOS.Backend.WorkflowRuntime.Dapr;

public sealed class GovernedWorkflowOrchestrator : Workflow<WorkflowOrchestrationInput, WorkflowOrchestrationOutput>
{
    public override async Task<WorkflowOrchestrationOutput> RunAsync(
        WorkflowContext context,
        WorkflowOrchestrationInput input)
    {
        var state = WorkflowOrchestrationCoordinator.CreateInitialState(
            input.InputContextJson,
            input.SafeModeActive);

        while (state.CompletedStepKeys.Count < input.Steps.Count && !state.StopWorkflow)
        {
            var nextStep = WorkflowOrchestrationCoordinator.PickNextStep(input.Steps, state.CompletedStepKeys);
            if (nextStep is null)
            {
                break;
            }

            var activityInput = new WorkflowStepActivityInput(
                input.WorkflowRunId,
                input.WorkflowVersionId,
                input.TenantId,
                input.UserId,
                input.Payload,
                nextStep,
                state.AccumulatedContextJson,
                input.Mode,
                input.IsPreview,
                input.IsDryRun,
                input.SafeModeActive);

            var activityOutput = await context.CallActivityAsync<WorkflowStepActivityOutput>(
                nameof(ExecuteGovernedWorkflowStepActivity),
                activityInput);

            WorkflowOrchestrationCoordinator.ApplyStepResult(
                state,
                activityOutput.Result,
                input.AllowPartialCompletion);

            if (state.StopWorkflow && !input.AllowPartialCompletion)
            {
                break;
            }
        }

        return WorkflowOrchestrationCoordinator.ToOrchestrationOutput(input, state);
    }
}
