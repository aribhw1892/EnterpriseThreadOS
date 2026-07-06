using Dapr.Client;
using Dapr.Workflow;
using ETOS.Backend.Identity;
using ETOS.Backend.WorkflowRuntime.Dapr;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class DaprWorkflowRuntimeAdapter(
    IOptions<WorkflowRuntimeOptions> options,
    IServiceProvider serviceProvider) : IWorkflowRuntimeAdapter
{
    public string AdapterKey => WorkflowRuntimeAdapterKeys.Dapr;

    public async Task<WorkflowRuntimeStartResult> StartManualRunAsync(
        WorkflowRuntimeStartRequest request,
        CancellationToken cancellationToken)
    {
        var runtimeOptions = options.Value;
        if (!runtimeOptions.EnableDaprHost)
        {
            throw new RequestValidationException(
                "WorkflowRuntime:EnableDaprHost must be true to use the dapr-v1 adapter. " +
                "Set WorkflowRuntime:AdapterKey to in-process-v1 for local development without a Dapr sidecar.");
        }

        var workflowClient = serviceProvider.GetService<DaprWorkflowClient>();
        if (workflowClient is null)
        {
            throw new RequestValidationException(
                "Dapr workflow client is not registered. Start the backend with `dapr run` and " +
                "WorkflowRuntime:EnableDaprHost=true, or use WorkflowRuntime:AdapterKey=in-process-v1.");
        }

        var input = WorkflowOrchestrationCoordinator.ToOrchestrationInput(request);
        var instanceId = request.WorkflowRunId.ToString();

        try
        {
            await workflowClient.ScheduleNewWorkflowAsync(
                nameof(GovernedWorkflowOrchestrator),
                instanceId,
                input);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, runtimeOptions.CompletionTimeoutSeconds)));

            var workflowState = await workflowClient.WaitForWorkflowCompletionAsync(
                instanceId,
                getInputsAndOutputs: true,
                timeoutCts.Token);

            if (!workflowState.Exists)
            {
                throw new RequestValidationException(
                    $"Dapr workflow instance '{instanceId}' was not found. Ensure daprd is running with workflow components.");
            }

            if (workflowState.RuntimeStatus == WorkflowRuntimeStatus.Failed)
            {
                var failureMessage = workflowState.FailureDetails?.ErrorMessage
                    ?? "Dapr workflow execution failed without details.";
                throw new RequestValidationException($"Dapr workflow execution failed: {failureMessage}");
            }

            if (workflowState.RuntimeStatus == WorkflowRuntimeStatus.Terminated)
            {
                throw new RequestValidationException($"Dapr workflow instance '{instanceId}' was terminated.");
            }

            var output = workflowState.ReadOutputAs<WorkflowOrchestrationOutput>();
            if (output is null)
            {
                throw new RequestValidationException(
                    $"Dapr workflow instance '{instanceId}' completed without output payload.");
            }

            return WorkflowOrchestrationCoordinator.ToRuntimeResult(input, output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RequestValidationException(
                $"Dapr workflow instance '{instanceId}' did not complete within {runtimeOptions.CompletionTimeoutSeconds} seconds.");
        }
        catch (RpcException exception)
        {
            throw new RequestValidationException(
                "Unable to reach the Dapr sidecar for workflow execution. Start daprd with `dapr run` " +
                $"and verify WorkflowRuntime:EnableDaprHost=true. ({exception.Status.Detail})");
        }
    }
}
