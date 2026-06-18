using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentRuntime;

public sealed class AgentRuntimeAdapterSelector(IEnumerable<IAgentRuntimeAdapter> adapters) : IAgentRuntimeAdapterSelector
{
    private readonly IReadOnlyDictionary<string, IAgentRuntimeAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.AdapterKey, StringComparer.OrdinalIgnoreCase);

    public IAgentRuntimeAdapter Resolve(string adapterKey)
    {
        var normalizedKey = adapterKey.Trim();
        if (!_adapters.TryGetValue(normalizedKey, out var adapter))
        {
            throw new RequestValidationException($"Agent runtime adapter '{normalizedKey}' is not registered.");
        }

        return adapter;
    }

    public Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var adapterKey = string.IsNullOrWhiteSpace(request.RequestedAdapterKey)
            ? AgentRuntimeAdapterKeys.PydanticAi
            : request.RequestedAdapterKey.Trim();

        return Resolve(adapterKey).ExecuteAsync(request, cancellationToken);
    }
}
