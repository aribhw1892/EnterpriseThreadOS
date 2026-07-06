namespace ETOS.Backend.WorkflowRuntime;

public sealed class InProcessWorkflowRuntimeAdapter(WorkflowOrchestrationCoordinator coordinator) : IWorkflowRuntimeAdapter
{
    public string AdapterKey => WorkflowRuntimeAdapterKeys.InProcess;

    public Task<WorkflowRuntimeStartResult> StartManualRunAsync(
        WorkflowRuntimeStartRequest request,
        CancellationToken cancellationToken)
        => coordinator.RunAsync(request, cancellationToken);
}
