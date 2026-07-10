using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports;

internal static class ImportFlatMetadataHelper
{
    private static readonly HashSet<string> LifecycleSourceHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "lifecycle",
        "status",
        "workflow",
        "lifecycleState",
        "lifecycle_state"
    };

    internal static string ResolveFlatImportObjectType(ImportMappingVersion mapping)
    {
        return mapping.ColumnMappings.FirstOrDefault(item => item.IsIdentityField)?.CanonicalObjectType
            ?? mapping.ColumnMappings.Select(item => item.CanonicalObjectType).FirstOrDefault()
            ?? string.Empty;
    }

    internal static IReadOnlyCollection<string> ResolveRequiredMetadataKeys(
        ModelPackageImportProfile profile,
        string canonicalObjectType)
    {
        foreach (var entry in profile.FlatImportMetadataPolicies)
        {
            if (string.Equals(entry.Key, canonicalObjectType, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value.RequiredMetadataKeys;
            }
        }

        return [];
    }

    internal static string? ResolveMetadataValue(
        string metadataKey,
        IReadOnlyDictionary<string, string?> row,
        ImportMappingVersion mapping,
        string canonicalObjectType)
    {
        if (string.Equals(metadataKey, "lifecycleState", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveLifecycleValue(row, mapping);
        }

        var attributeMapping = mapping.ColumnMappings.FirstOrDefault(item =>
            string.Equals(item.CanonicalObjectType, canonicalObjectType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.CanonicalAttributeKey, metadataKey, StringComparison.OrdinalIgnoreCase));
        if (attributeMapping is not null
            && row.TryGetValue(attributeMapping.SourceColumn, out var mappedValue)
            && !string.IsNullOrWhiteSpace(mappedValue))
        {
            return mappedValue;
        }

        return null;
    }

    internal static string? ResolveLifecycleValue(IReadOnlyDictionary<string, string?> row, ImportMappingVersion mapping)
    {
        foreach (var lifecycleMapping in mapping.LifecycleMappings)
        {
            if (row.Values.Any(value =>
                    !string.IsNullOrWhiteSpace(value)
                    && NormalizeImportKey(value!) == lifecycleMapping.NormalizedSourceValue))
            {
                return lifecycleMapping.CanonicalLifecycleKey;
            }
        }

        return null;
    }

    internal static bool HasUnmappedLifecycleSourceSignal(
        IReadOnlyDictionary<string, string?> row,
        ImportMappingVersion mapping)
    {
        foreach (var (header, value) in row)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (LifecycleSourceHeaderNames.Contains(header))
            {
                return true;
            }
        }

        foreach (var columnMapping in mapping.ColumnMappings.Where(item =>
                     item.NormalizedCanonicalAttributeKey is "status" or "lifecyclestate" or "workflow"))
        {
            if (row.TryGetValue(columnMapping.SourceColumn, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeImportKey(string value) => value.Trim().ToUpperInvariant();
}
