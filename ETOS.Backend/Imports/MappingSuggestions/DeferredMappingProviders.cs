using ETOS.Backend.Identity;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed class HermesMappingProvider : IMappingSuggestionProvider
{
    public string ProviderKey => MappingSuggestionProviderKeys.Hermes;

    public Task<ImportMappingSuggestionResult> SuggestAsync(
        ImportMappingSuggestionRequest request,
        CancellationToken cancellationToken)
        => throw new RequestValidationException("Hermes mapping suggestions are deferred and not available in this release.");
}
