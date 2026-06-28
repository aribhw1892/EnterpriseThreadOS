using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentRuntime;

public static class AgentRuntimeAdapterKeys
{
    public const string PydanticAi = "pydantic-ai-v1";
    public const string Hermes = "hermes-v1";
    public const string LangGraph = "langgraph-v1";

    public static readonly IReadOnlyCollection<string> All =
    [
        PydanticAi,
        Hermes,
        LangGraph
    ];
}

public static class AgentRuntimeExecutionStatuses
{
    public const string Disabled = "Disabled";
    public const string Deferred = "Deferred";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public sealed record AgentRuntimeExecutionRequest(
    Guid TenantId,
    Guid UserId,
    Guid? AgentTemplateVersionId,
    string? GovernedContextSummaryJson,
    string? StructuredInputJson,
    bool PreviewMode,
    string? RequestedAdapterKey = null,
    Guid? AgentVersionId = null,
    Guid? AgentRunId = null,
    string? PromptTemplatePayloadJson = null,
    string? OutputSchemaJson = null,
    string? PrimaryModelProviderKey = null,
    string? PrimaryModelId = null,
    string? FallbackModelsJson = null,
    string? ToolOutputSummariesJson = null);

public sealed record AgentRuntimeExecutionResult(
    string AdapterKey,
    string Status,
    string? StructuredOutputJson,
    IReadOnlyCollection<string> TraceNotes,
    string? ModelUsed = null,
    string? FallbackAppliedJson = null);

public interface IAgentRuntimeAdapter
{
    string AdapterKey { get; }

    Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken);
}

public interface IAgentRuntimeAdapterSelector
{
    IAgentRuntimeAdapter Resolve(string adapterKey);

    Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken);
}
