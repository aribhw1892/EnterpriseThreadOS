namespace ETOS.Backend.GraphMemory;

public static class GraphIdentityKeyBuilder
{
    public static string? Build(
        string? sourceSystem,
        string objectType,
        IReadOnlyDictionary<string, string?> identityAttributes)
    {
        if (identityAttributes.Count == 0)
        {
            return null;
        }

        var attributeParts = identityAttributes
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{NormalizeToken(item.Key)}={NormalizeToken(item.Value!)}")
            .ToList();
        if (attributeParts.Count == 0)
        {
            return null;
        }

        return $"{NormalizeToken(sourceSystem ?? string.Empty)}|{NormalizeToken(objectType)}|{string.Join(";", attributeParts)}";
    }

    private static string NormalizeToken(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
