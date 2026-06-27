using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed record ImportMappingSuggestionRequest(
    IReadOnlyCollection<string> Headers,
    IReadOnlyCollection<IReadOnlyDictionary<string, string?>> SampleRows,
    ResolvedModelPackageContext ModelContext,
    string? RequestedProviderKey = null,
    bool IncludeDiagnostics = false,
    string? MappingAssistantAgentKey = null,
    Guid? MappingAssistantAgentVersionId = null);

public sealed record ImportMappingSuggestionResult(
    string ProviderKey,
    IReadOnlyCollection<ImportColumnMappingSuggestionResponse> ColumnSuggestions,
    IReadOnlyCollection<ImportLifecycleMappingSuggestionResponse> LifecycleSuggestions,
    ImportMappingSuggestionDiagnostics? Diagnostics = null);

public interface IMappingSuggestionProvider
{
    string ProviderKey { get; }
    Task<ImportMappingSuggestionResult> SuggestAsync(
        ImportMappingSuggestionRequest request,
        CancellationToken cancellationToken);
}

public interface IMappingSuggestionProviderSelector
{
    Task<ImportMappingSuggestionResult> SuggestAsync(
        ImportMappingSuggestionRequest request,
        CancellationToken cancellationToken);
}

public static class MappingSuggestionProviderKeys
{
    public const string RuleBased = "rule-based-v1";
    public const string PydanticAi = "pydantic-ai-v1";
    public const string Hermes = "hermes-v1";
}
