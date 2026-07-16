using System.Text.Json;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports.MappingSuggestions;

public static class MappingSuggestionContextBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string BuildGovernedContextJson(ResolvedModelPackageContext modelContext)
    {
        var payload = new
        {
            modelPackageKey = modelContext.ModelPackage.Key,
            modelPackageVersionLabel = modelContext.ModelPackage.VersionLabel,
            objectTypes = modelContext.Ontology.ObjectTypes
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new
                {
                    key = item.Key,
                    displayName = item.DisplayName,
                    safeSummary = item.SafeSummary
                }),
            attributes = modelContext.AttributeSchema.Attributes
                .OrderBy(item => item.AttributeKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new
                {
                    attributeKey = item.AttributeKey,
                    appliesToObjectType = item.AppliesToObjectType,
                    displayName = item.DisplayName,
                    isRequired = item.IsRequired,
                    safeSummary = item.SafeSummary
                }),
            lifecycleStates = modelContext.LifecycleVocabulary.States
                .OrderBy(item => item.SortOrder)
                .Select(item => new
                {
                    key = item.Key,
                    displayName = item.DisplayName,
                    sortOrder = item.SortOrder
                })
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string BuildStructuredInputJson(ImportMappingSuggestionRequest request)
    {
        var modelContext = request.ModelContext;
        var payload = new
        {
            headers = request.Headers,
            sampleRows = request.SampleRows,
            allowedObjectTypes = modelContext.Ontology.ObjectTypes
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Key)
                .ToArray(),
            allowedAttributes = modelContext.AttributeSchema.Attributes
                .OrderBy(item => item.AttributeKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new
                {
                    attributeKey = item.AttributeKey,
                    appliesToObjectType = item.AppliesToObjectType
                })
                .ToArray(),
            allowedLifecycleKeys = modelContext.LifecycleVocabulary.States
                .OrderBy(item => item.SortOrder)
                .Select(item => item.Key)
                .ToArray()
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
