using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports.MappingSuggestions;

public static class MappingSuggestionOutputSchemaFactory
{
    public static string Build(ResolvedModelPackageContext modelContext)
    {
        var root = JsonNode.Parse(MappingSuggestionOutputSchema.Json) as JsonObject
            ?? throw new InvalidOperationException("Base mapping suggestion output schema is invalid.");

        var columnItems = root["properties"]?["columnSuggestions"]?["items"] as JsonObject
            ?? throw new InvalidOperationException("Mapping suggestion schema is missing columnSuggestions.items.");
        var columnProperties = columnItems["properties"] as JsonObject
            ?? throw new InvalidOperationException("Mapping suggestion schema is missing columnSuggestions.items.properties.");

        var lifecycleItems = root["properties"]?["lifecycleSuggestions"]?["items"] as JsonObject
            ?? throw new InvalidOperationException("Mapping suggestion schema is missing lifecycleSuggestions.items.");
        var lifecycleProperties = lifecycleItems["properties"] as JsonObject
            ?? throw new InvalidOperationException("Mapping suggestion schema is missing lifecycleSuggestions.items.properties.");

        var objectTypeKeys = modelContext.Ontology.ObjectTypes
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var attributeKeys = modelContext.AttributeSchema.Attributes
            .Select(item => item.AttributeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lifecycleKeys = modelContext.LifecycleVocabulary.States
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SetEnum(columnProperties, "canonicalObjectType", objectTypeKeys);
        SetEnum(columnProperties, "canonicalAttributeKey", attributeKeys);
        SetEnum(lifecycleProperties, "canonicalLifecycleKey", lifecycleKeys);

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void SetEnum(JsonObject properties, string propertyName, IReadOnlyCollection<string> values)
    {
        if (properties[propertyName] is not JsonObject property)
        {
            property = new JsonObject { ["type"] = "string" };
            properties[propertyName] = property;
        }

        var enumArray = new JsonArray();
        foreach (var value in values)
        {
            enumArray.Add(value);
        }

        property["enum"] = enumArray;
    }
}
