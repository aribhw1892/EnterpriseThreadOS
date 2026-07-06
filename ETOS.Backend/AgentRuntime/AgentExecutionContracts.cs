namespace ETOS.Backend.AgentRuntime;

public sealed record AgentExecutionRequest(
    string? StructuredInputJson,
    string? QueryText,
    Guid? StartGraphNodeId = null,
    Guid? DocumentArtifactId = null,
    Guid? ParentWorkflowRunId = null);

public sealed record AgentExecutionResponse(
    Guid AgentRunId,
    string Status,
    bool IsPreview,
    bool IsDryRun,
    string? StructuredOutputJson,
    string? OutputSafeSummaryJson,
    Guid? RecommendationArtifactId,
    Guid? RecommendationVersionId,
    Guid? AiTraceRecordId,
    Guid? RetrievalRunId,
    IReadOnlyCollection<Guid> ToolRunIds,
    IReadOnlyCollection<string> ValidationNotes);
