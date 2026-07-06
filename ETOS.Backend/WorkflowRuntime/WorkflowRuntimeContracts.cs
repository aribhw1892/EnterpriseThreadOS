using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuns;

namespace ETOS.Backend.WorkflowRuntime;

public static class WorkflowRuntimeAdapterKeys
{
    public const string InProcess = "in-process-v1";
    public const string Dapr = "dapr-v1";

    public static readonly IReadOnlyCollection<string> All = [InProcess, Dapr];
}

public enum WorkflowExecutionMode
{
    Preview = 0,
    TestRun = 1,
    Execute = 2
}

public sealed record WorkflowExecutionRequest(string? StructuredInputJson);

public sealed record WorkflowExecutionResponse(
    Guid WorkflowRunId,
    string Status,
    bool IsPreview,
    bool SafeModeApplied,
    bool PartialCompletion,
    string? OutputSafeSummaryJson,
    Guid? AiTraceRecordId,
    Guid? AuditRecordId,
    IReadOnlyCollection<Guid> RecommendationArtifactIds,
    IReadOnlyCollection<Guid> ReviewTaskArtifactIds,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record WorkflowRuntimeStartRequest(
    Guid WorkflowRunId,
    Guid WorkflowVersionId,
    Guid WorkflowArtifactId,
    Guid TenantId,
    Guid UserId,
    WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument Payload,
    string InputContextJson,
    WorkflowExecutionMode Mode,
    bool IsPreview,
    bool IsDryRun,
    bool SafeModeActive,
    bool AllowPartialCompletion);

public sealed record WorkflowStepExecutionResult(
    string StepKey,
    string Status,
    string? OutputContextJson,
    Guid? AgentRunId,
    Guid? ToolRunId,
    Guid? RecommendationArtifactId,
    Guid? ReviewTaskArtifactId,
    SafeModeEvent? SafeModeEvent);

public sealed record WorkflowRuntimeStartResult(
    string Status,
    string? OutputContextJson,
    string? StepResultsJson,
    IReadOnlyCollection<Guid> RecommendationArtifactIds,
    IReadOnlyCollection<Guid> ReviewTaskArtifactIds,
    IReadOnlyCollection<SafeModeEvent> SafeModeEvents,
    bool PartialCompletion,
    bool SafeModeApplied);

public sealed record WorkflowStepExecutionContext(
    Guid WorkflowRunId,
    Guid WorkflowVersionId,
    Guid TenantId,
    Guid UserId,
    WorkflowDefinitionPayloadParser.WorkflowStepDocument Step,
    WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument Payload,
    string AccumulatedContextJson,
    WorkflowExecutionMode Mode,
    bool IsPreview,
    bool IsDryRun,
    bool SafeModeActive,
    bool StepBlockedBySafeMode);

public interface IWorkflowRuntimeAdapter
{
    string AdapterKey { get; }

    Task<WorkflowRuntimeStartResult> StartManualRunAsync(
        WorkflowRuntimeStartRequest request,
        CancellationToken cancellationToken);
}

public interface IWorkflowRuntimeAdapterSelector
{
    IWorkflowRuntimeAdapter Resolve(string adapterKey);
}

public interface IWorkflowStepExecutor
{
    Task<WorkflowStepExecutionResult> ExecuteAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken);
}

public interface IBusinessPolicyWorkflowEvaluator
{
    Task<BusinessPolicyWorkflowEvaluationResult> EvaluateAsync(
        Guid businessPolicyDefinitionVersionId,
        string contextJson,
        CancellationToken cancellationToken);
}

public sealed record BusinessPolicyWorkflowEvaluationResult(
    bool Passed,
    string? FailedRuleKey,
    string? Reason);

public interface IGovernedOptimizationEvaluationService
{
    Task<GovernedOptimizationEvaluationResult> EvaluateAsync(
        Guid optimizationModelVersionId,
        string contextJson,
        CancellationToken cancellationToken);
}

public sealed record GovernedOptimizationEvaluationResult(
    bool Succeeded,
    string EvaluationResultJson,
    string? Reason);

public interface IWorkflowExecutionService
{
    Task<WorkflowExecutionResponse> PreviewAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken);

    Task<WorkflowExecutionResponse> TestRunAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken);

    Task<WorkflowExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken);
}
