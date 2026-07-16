using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed class MappingSuggestionOptions
{
    public const string SectionName = "ImportMappingSuggestions";

    public string DefaultProviderKey { get; set; } = MappingSuggestionProviderKeys.RuleBased;

    public bool Enabled { get; set; }

    public string? MappingAssistantAgentKey { get; set; } = "import-mapping-assistant";

    /// <summary>
    /// When enabled, falls back to rule-based suggestions when runtime execution fails,
    /// when structured LLM output is missing usable column attribute mappings,
    /// or when LLM output references ontology-invalid object types, attributes, or lifecycle keys.
    /// </summary>
    public bool FallbackToRuleBasedOnRuntimeFailure { get; set; }
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
