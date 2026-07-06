using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuns;

namespace ETOS.Backend.WorkflowRuntime;

public sealed record WorkflowOrchestrationInput(
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
    bool AllowPartialCompletion,
    IReadOnlyList<WorkflowDefinitionPayloadParser.WorkflowStepDocument> Steps);

public sealed record WorkflowOrchestrationOutput(
    string Status,
    string? OutputContextJson,
    string? StepResultsJson,
    IReadOnlyList<Guid> RecommendationArtifactIds,
    IReadOnlyList<Guid> ReviewTaskArtifactIds,
    IReadOnlyList<SafeModeEventSnapshot> SafeModeEvents,
    bool PartialCompletion,
    bool SafeModeApplied);

public sealed record WorkflowStepActivityInput(
    Guid WorkflowRunId,
    Guid WorkflowVersionId,
    Guid TenantId,
    Guid UserId,
    WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument Payload,
    WorkflowDefinitionPayloadParser.WorkflowStepDocument Step,
    string AccumulatedContextJson,
    WorkflowExecutionMode Mode,
    bool IsPreview,
    bool IsDryRun,
    bool SafeModeActive);

public sealed record WorkflowStepActivityOutput(
    WorkflowStepExecutionResult Result);

public sealed class WorkflowOrchestrationState
{
    public HashSet<string> CompletedStepKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string AccumulatedContextJson { get; set; } = "{}";

    public List<WorkflowStepExecutionResult> StepResults { get; init; } = [];

    public List<Guid> RecommendationArtifactIds { get; init; } = [];

    public List<Guid> ReviewTaskArtifactIds { get; init; } = [];

    public List<SafeModeEvent> SafeModeEvents { get; init; } = [];

    public bool SafeModeApplied { get; set; }

    public bool StopWorkflow { get; set; }
}

public sealed record SafeModeEventSnapshot(
    string StepKey,
    string EventKind,
    string Reason,
    string? PolicyRuleKey,
    string? BlockedAction,
    Guid? AgentRunId,
    Guid? ToolRunId,
    Guid? ReviewTaskArtifactId);
