using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed class RuleBasedMappingProvider : IMappingSuggestionProvider
{
    public string ProviderKey => MappingSuggestionProviderKeys.RuleBased;

    public Task<ImportMappingSuggestionResult> SuggestAsync(
        ImportMappingSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var columnSuggestions = BuildColumnSuggestions(request.Headers, request.ModelContext).ToList();
        var lifecycleSuggestions = BuildLifecycleSuggestions(request.Headers, request.SampleRows, request.ModelContext, columnSuggestions).ToList();
        ImportMappingSuggestionDiagnostics? diagnostics = null;
        if (request.IncludeDiagnostics)
        {
            diagnostics = new ImportMappingSuggestionDiagnostics(
                ProviderKey,
                RuntimeCalled: false,
                null,
                null,
                null,
                null,
                [],
                PrefetchAttempted: false,
                PrefetchSucceeded: false,
                null,
                null,
                null,
                null,
                null,
                MappingSuggestionContextBuilder.BuildGovernedContextJson(request.ModelContext),
                MappingSuggestionContextBuilder.BuildStructuredInputJson(request),
                null,
                null,
                null,
                null,
                null,
                null,
                UsedRuleBasedFallback: false,
                ErrorMessage: null);
        }

        return Task.FromResult(new ImportMappingSuggestionResult(ProviderKey, columnSuggestions, lifecycleSuggestions, diagnostics));
    }

    internal static IEnumerable<ImportColumnMappingSuggestionResponse> BuildColumnSuggestions(
        IReadOnlyCollection<string> headers,
        ResolvedModelPackageContext modelContext)
    {
        var attributes = modelContext.AttributeSchema.Attributes.ToList();
        var firstObjectType = modelContext.Ontology.ObjectTypes.OrderBy(item => item.Key).FirstOrDefault()
            ?? throw new RequestValidationException("Active model package ontology does not define object types.");
        foreach (var header in headers)
        {
            var normalizedHeader = NormalizeLoose(header);
            var attribute = attributes.FirstOrDefault(item => NormalizeLoose(item.AttributeKey) == normalizedHeader)
                ?? attributes.FirstOrDefault(item => NormalizeLoose(item.DisplayName ?? item.AttributeKey) == normalizedHeader)
                ?? attributes.FirstOrDefault(item => normalizedHeader.Contains(NormalizeLoose(item.AttributeKey), StringComparison.Ordinal));
            var objectType = attribute?.AppliesToObjectType ?? firstObjectType.Key;
            var isIdentity = normalizedHeader.Contains("id", StringComparison.Ordinal)
                || normalizedHeader.Contains("number", StringComparison.Ordinal)
                || normalizedHeader.EndsWith("no", StringComparison.Ordinal);
            yield return new ImportColumnMappingSuggestionResponse(
                header,
                objectType,
                attribute?.AttributeKey,
                isIdentity,
                attribute?.IsRequired ?? isIdentity,
                attribute is null ? 0.45m : 0.85m,
                attribute is null ? "Column matched to the first canonical object type by heuristic fallback." : "Column matched by canonical attribute name.");
        }
    }

    internal static IEnumerable<ImportLifecycleMappingSuggestionResponse> BuildLifecycleSuggestions(
        IReadOnlyCollection<string> headers,
        IReadOnlyCollection<IReadOnlyDictionary<string, string?>> sampleRows,
        ResolvedModelPackageContext modelContext,
        IReadOnlyCollection<ImportColumnMappingSuggestionResponse> columnSuggestions)
    {
        var lifecycleColumn = headers.FirstOrDefault(header => NormalizeLoose(header).Contains("lifecycle", StringComparison.Ordinal))
            ?? headers.FirstOrDefault(header => NormalizeLoose(header).Contains("status", StringComparison.Ordinal))
            ?? headers.FirstOrDefault(header => NormalizeLoose(header).Contains("state", StringComparison.Ordinal));
        if (lifecycleColumn is null)
        {
            yield break;
        }

        var canonicalStates = modelContext.LifecycleVocabulary.States.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceValues = sampleRows
            .Select(row => row.TryGetValue(lifecycleColumn, out var value) ? value?.Trim() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var sourceValue in sourceValues)
        {
            var canonical = canonicalStates.Contains(sourceValue!)
                ? modelContext.LifecycleVocabulary.States.First(item => string.Equals(item.Key, sourceValue, StringComparison.OrdinalIgnoreCase)).Key
                : modelContext.LifecycleVocabulary.States.OrderBy(item => item.SortOrder).First().Key;
            yield return new ImportLifecycleMappingSuggestionResponse(
                sourceValue!,
                canonical,
                canonicalStates.Contains(sourceValue!) ? 0.9m : 0.5m,
                canonicalStates.Contains(sourceValue!)
                    ? "Source lifecycle value matched a canonical lifecycle state."
                    : "Source lifecycle value mapped to the default canonical lifecycle state.");
        }
    }

    private static string NormalizeLoose(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
