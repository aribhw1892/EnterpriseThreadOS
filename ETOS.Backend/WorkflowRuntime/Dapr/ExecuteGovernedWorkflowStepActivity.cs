using Dapr.Workflow;

namespace ETOS.Backend.WorkflowRuntime.Dapr;

public sealed class ExecuteGovernedWorkflowStepActivity(WorkflowOrchestrationCoordinator coordinator)
    : WorkflowActivity<WorkflowStepActivityInput, WorkflowStepActivityOutput>
{
    public override Task<WorkflowStepActivityOutput> RunAsync(
        WorkflowActivityContext context,
        WorkflowStepActivityInput input)
        => coordinator.RunSingleStepAsync(input, CancellationToken.None);
}
