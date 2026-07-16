using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed record MappingSuggestionSanitizeResult(
    IReadOnlyList<ImportColumnMappingSuggestionResponse> ColumnSuggestions,
    IReadOnlyList<ImportLifecycleMappingSuggestionResponse> LifecycleSuggestions,
    IReadOnlyList<string> Issues);

public static class MappingSuggestionOntologyValidator
{
    public static void Validate(
        IReadOnlyCollection<ImportColumnMappingSuggestionResponse> columnSuggestions,
        IReadOnlyCollection<ImportLifecycleMappingSuggestionResponse> lifecycleSuggestions,
        ResolvedModelPackageContext modelContext)
    {
        var sanitize = Sanitize(columnSuggestions, lifecycleSuggestions, modelContext);
        if (sanitize.Issues.Count == 0)
        {
            return;
        }

        throw new RequestValidationException(sanitize.Issues[0]);
    }

    public static MappingSuggestionSanitizeResult Sanitize(
        IReadOnlyCollection<ImportColumnMappingSuggestionResponse> columnSuggestions,
        IReadOnlyCollection<ImportLifecycleMappingSuggestionResponse> lifecycleSuggestions,
        ResolvedModelPackageContext modelContext)
    {
        var objectTypes = modelContext.Ontology.ObjectTypes
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var attributes = modelContext.AttributeSchema.Attributes
            .ToLookup(item => item.AppliesToObjectType, StringComparer.OrdinalIgnoreCase);
        var lifecycleKeys = modelContext.LifecycleVocabulary.States
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var issues = new List<string>();
        var cleanedColumns = new List<ImportColumnMappingSuggestionResponse>();
        var cleanedLifecycle = new List<ImportLifecycleMappingSuggestionResponse>();

        foreach (var suggestion in columnSuggestions)
        {
            if (!objectTypes.Contains(suggestion.CanonicalObjectType))
            {
                issues.Add(
                    $"Mapping suggestion references unknown object type '{suggestion.CanonicalObjectType}'.");
                continue;
            }

            if (suggestion.CanonicalAttributeKey is not null
                && !attributes[suggestion.CanonicalObjectType].Any(item =>
                    string.Equals(item.AttributeKey, suggestion.CanonicalAttributeKey, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(
                    $"Mapping suggestion references unknown attribute '{suggestion.CanonicalAttributeKey}' for object type '{suggestion.CanonicalObjectType}'.");
                cleanedColumns.Add(suggestion with
                {
                    CanonicalAttributeKey = null,
                    Confidence = Math.Min(suggestion.Confidence, 0.3m),
                    Rationale = AppendRationaleNote(
                        suggestion.Rationale,
                        $"cleared invalid attribute '{suggestion.CanonicalAttributeKey}'")
                });
                continue;
            }

            cleanedColumns.Add(suggestion);
        }

        foreach (var suggestion in lifecycleSuggestions)
        {
            if (!lifecycleKeys.Contains(suggestion.CanonicalLifecycleKey))
            {
                issues.Add(
                    $"Mapping suggestion references unknown lifecycle key '{suggestion.CanonicalLifecycleKey}'.");
                continue;
            }

            cleanedLifecycle.Add(suggestion);
        }

        return new MappingSuggestionSanitizeResult(cleanedColumns, cleanedLifecycle, issues);
    }

    public static ImportColumnMappingSuggestionResponse ClampColumn(ImportColumnMappingSuggestionResponse suggestion) =>
        suggestion with { Confidence = ClampConfidence(suggestion.Confidence) };

    public static ImportLifecycleMappingSuggestionResponse ClampLifecycle(ImportLifecycleMappingSuggestionResponse suggestion) =>
        suggestion with { Confidence = ClampConfidence(suggestion.Confidence) };

    private static string AppendRationaleNote(string rationale, string note)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return $"[{note}]";
        }

        return $"{rationale} [{note}]";
    }

    private static decimal ClampConfidence(decimal confidence) =>
        confidence switch
        {
            < 0m => 0m,
            > 1m => 1m,
            _ => confidence
        };
}
