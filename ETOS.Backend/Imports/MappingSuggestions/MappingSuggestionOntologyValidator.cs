using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports.MappingSuggestions;

public static class MappingSuggestionOntologyValidator
{
    public static void Validate(
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

        foreach (var suggestion in columnSuggestions)
        {
            if (!objectTypes.Contains(suggestion.CanonicalObjectType))
            {
                throw new RequestValidationException(
                    $"Mapping suggestion references unknown object type '{suggestion.CanonicalObjectType}'.");
            }

            if (suggestion.CanonicalAttributeKey is not null
                && !attributes[suggestion.CanonicalObjectType].Any(item =>
                    string.Equals(item.AttributeKey, suggestion.CanonicalAttributeKey, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RequestValidationException(
                    $"Mapping suggestion references unknown attribute '{suggestion.CanonicalAttributeKey}' for object type '{suggestion.CanonicalObjectType}'.");
            }
        }

        foreach (var suggestion in lifecycleSuggestions)
        {
            if (!lifecycleKeys.Contains(suggestion.CanonicalLifecycleKey))
            {
                throw new RequestValidationException(
                    $"Mapping suggestion references unknown lifecycle key '{suggestion.CanonicalLifecycleKey}'.");
            }
        }
    }

    public static ImportColumnMappingSuggestionResponse ClampColumn(ImportColumnMappingSuggestionResponse suggestion) =>
        suggestion with { Confidence = ClampConfidence(suggestion.Confidence) };

    public static ImportLifecycleMappingSuggestionResponse ClampLifecycle(ImportLifecycleMappingSuggestionResponse suggestion) =>
        suggestion with { Confidence = ClampConfidence(suggestion.Confidence) };

    private static decimal ClampConfidence(decimal confidence) =>
        confidence switch
        {
            < 0m => 0m,
            > 1m => 1m,
            _ => confidence
        };
}
