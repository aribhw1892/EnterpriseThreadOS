using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed class MappingSuggestionOptions
{
    public const string SectionName = "ImportMappingSuggestions";

    public string DefaultProviderKey { get; set; } = MappingSuggestionProviderKeys.RuleBased;

    public bool Enabled { get; set; }

    public string RuntimeAdapterKey { get; set; } = AgentRuntime.AgentRuntimeAdapterKeys.PydanticAi;

    public string PrimaryModelProviderKey { get; set; } = "openai";

    public string PrimaryModelId { get; set; } = "gpt-4o-mini";

    public IReadOnlyCollection<MappingSuggestionFallbackModel>? FallbackModels { get; set; }

    public string PromptTemplateBody { get; set; } =
        """
        You are an import mapping assistant for EnterpriseThreadOS.
        Analyze CSV headers and sample rows against the governed ontology context.
        Suggest column mappings to canonical object types and attributes, and lifecycle value mappings when applicable.
        Consider tool outputs as deterministic hints; override with rationale when appropriate.
        Return JSON only that matches the output schema.
        """;

    public bool FallbackToRuleBasedOnRuntimeFailure { get; set; }

    public bool PrefetchToolEnabled { get; set; } = true;

    public string PrefetchToolKey { get; set; } = "mapping-predictor-tool";
}

public sealed class MappingSuggestionFallbackModel
{
    public string ProviderKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string? TriggerReason { get; set; }
}

public sealed class MappingSuggestionProviderSelector(
    IEnumerable<IMappingSuggestionProvider> providers,
    IOptions<MappingSuggestionOptions> options) : IMappingSuggestionProviderSelector
{
    private readonly IReadOnlyDictionary<string, IMappingSuggestionProvider> _providers =
        providers.ToDictionary(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public Task<ImportMappingSuggestionResult> SuggestAsync(
        ImportMappingSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var providerKey = string.IsNullOrWhiteSpace(request.RequestedProviderKey)
            ? options.Value.DefaultProviderKey
            : request.RequestedProviderKey.Trim();

        if (!_providers.TryGetValue(providerKey, out var provider))
        {
            throw new RequestValidationException($"Mapping suggestion provider '{providerKey}' is not registered.");
        }

        return provider.SuggestAsync(request, cancellationToken);
    }
}
