using ETOS.Backend.AgentRuntime;

namespace ETOS.Backend.Tests.Fixtures;

internal sealed class MockAgentRuntimeAdapter : IAgentRuntimeAdapter
{
    public string AdapterKey => AgentRuntimeAdapterKeys.PydanticAi;

    public Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new AgentRuntimeExecutionResult(
            AdapterKey,
            AgentRuntimeExecutionStatuses.Succeeded,
            MockAgentRuntimeHttpHandler.ValidChatAnswerOutput,
            ["mock-agent-runtime"],
            "mock-v1",
            null));
}
