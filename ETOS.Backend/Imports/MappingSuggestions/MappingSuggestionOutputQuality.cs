namespace ETOS.Backend.Imports.MappingSuggestions;

public static class MappingSuggestionOutputQuality
{
    public static bool HasUsableColumnSuggestions(
        IReadOnlyCollection<ImportColumnMappingSuggestionResponse> columnSuggestions)
        => columnSuggestions.Any(item =>
            !string.IsNullOrWhiteSpace(item.CanonicalAttributeKey) || item.IsIdentityField);
}
