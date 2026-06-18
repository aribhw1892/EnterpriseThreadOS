using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentRuntime;

/// <summary>
/// First adapter key in PRD runtime-neutral strategy. Disabled until a future deployment configures PydanticAI HTTP execution (Issue 22).
/// </summary>
public sealed class PydanticAiRuntimeAdapter : IAgentRuntimeAdapter
{
    public string AdapterKey => AgentRuntimeAdapterKeys.PydanticAi;

    public Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
        => throw new RequestValidationException("PydanticAI agent runtime is not configured for this deployment.");
}

/// <summary>
/// Hermes adapter reserved for future enterprise agent runtime wiring. Deferred in Issue 18.4.
/// </summary>
public sealed class HermesRuntimeAdapter : IAgentRuntimeAdapter
{
    public string AdapterKey => AgentRuntimeAdapterKeys.Hermes;

    public Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
        => throw new RequestValidationException("Hermes agent runtime is deferred and not available in this release.");
}

/// <summary>
/// LangGraph adapter reserved for workflow-oriented agent orchestration (Issue 25). Deferred in Issue 18.4.
/// </summary>
public sealed class LangGraphRuntimeAdapter : IAgentRuntimeAdapter
{
    public string AdapterKey => AgentRuntimeAdapterKeys.LangGraph;

    public Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
        => throw new RequestValidationException("LangGraph agent runtime is deferred and not available in this release.");
}
