using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class WorkflowRuntimeAdapterSelector(
    IEnumerable<IWorkflowRuntimeAdapter> adapters,
    IOptions<WorkflowRuntimeOptions> options) : IWorkflowRuntimeAdapterSelector
{
    private readonly IReadOnlyDictionary<string, IWorkflowRuntimeAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.AdapterKey, StringComparer.OrdinalIgnoreCase);

    public IWorkflowRuntimeAdapter Resolve(string adapterKey)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(adapterKey)
            ? options.Value.AdapterKey.Trim()
            : adapterKey.Trim();

        if (!_adapters.TryGetValue(normalizedKey, out var adapter))
        {
            throw new RequestValidationException($"Workflow runtime adapter '{normalizedKey}' is not registered.");
        }

        return adapter;
    }
}
