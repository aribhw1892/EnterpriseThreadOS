using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Outcomes;

public static class OutcomeTaxonomyPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(OutcomeTaxonomyPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static OutcomeTaxonomyPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<OutcomeTaxonomyPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Outcome taxonomy payload is invalid.");
        return document;
    }

    public static OutcomeTaxonomyPayloadDocument Create(string taxonomyKey, IReadOnlyCollection<string> categories)
        => Normalize(new OutcomeTaxonomyPayloadDocument
        {
            TaxonomyKey = taxonomyKey.Trim(),
            Categories = categories.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        });

    public static void ValidateCore(OutcomeTaxonomyPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.TaxonomyKey))
        {
            throw new RequestValidationException("taxonomyKey is required.");
        }

        if (document.Categories is null || document.Categories.Count == 0)
        {
            throw new RequestValidationException("At least one outcome category is required.");
        }
    }

    private static OutcomeTaxonomyPayloadDocument Normalize(OutcomeTaxonomyPayloadDocument document)
    {
        document.TaxonomyKey = document.TaxonomyKey?.Trim() ?? string.Empty;
        document.Categories ??= [];
        return document;
    }

    public sealed class OutcomeTaxonomyPayloadDocument
    {
        public string? TaxonomyKey { get; set; }
        public List<string>? Categories { get; set; }
    }
}
