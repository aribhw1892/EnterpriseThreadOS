using System.Text.Json;
using System.Text.Json.Serialization;

namespace ETOS.Backend.Ontology;

public sealed class ModelPackageImportProfile
{
    public string? DefaultBomRelationshipType { get; init; }
    public IReadOnlyCollection<string> ParentColumnSynonyms { get; init; } = [];
    public IReadOnlyCollection<string> ChildColumnSynonyms { get; init; } = [];
    public IReadOnlyCollection<string> QuantityColumnSynonyms { get; init; } = [];
    public IReadOnlyCollection<string> UnitColumnSynonyms { get; init; } = [];
    public IReadOnlyCollection<string> UsageColumnSynonyms { get; init; } = [];
    public IReadOnlyCollection<string> ComparisonSideColumnSynonyms { get; init; } = [];
    public IReadOnlyCollection<StructuralComparisonSideProfile> ComparisonSides { get; init; } = [];
    public ModelPackageRecommendationTemplates? RecommendationTemplates { get; init; }
}

public sealed class StructuralComparisonSideProfile
{
    public required string Label { get; init; }
    public IReadOnlyCollection<string> Aliases { get; init; } = [];
}

public sealed class ModelPackageRecommendationTemplates
{
    public string? StructuralDriftTitle { get; init; }
    public string? StructuralDriftSummary { get; init; }
    public string? StructuralComparisonAuditSummary { get; init; }
    public string? ReviewPrimarySideActionTitle { get; init; }
    public string? ReviewPrimarySideActionCode { get; init; }
    public string? ReviewPrimarySideActionRationale { get; init; }
    public string? ReviewImpactActionTitle { get; init; }
    public string? ReviewImpactActionCode { get; init; }
    public string? ReviewImpactActionRationale { get; init; }
}

public sealed class ModelPackageQueryIntentExtensions
{
    public IReadOnlyDictionary<string, ModelPackageQueryIntentExtension> Intents { get; init; }
        = new Dictionary<string, ModelPackageQueryIntentExtension>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelPackageQueryIntentExtension
{
    public IReadOnlyCollection<string> RelationshipTypes { get; init; } = [];
    public string? Summary { get; init; }
}

public static class ModelPackageProfileParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ModelPackageImportProfile ParseImportProfile(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ModelPackageImportProfile();
        }

        return JsonSerializer.Deserialize<ModelPackageImportProfile>(json, JsonOptions)
            ?? new ModelPackageImportProfile();
    }

    public static ModelPackageQueryIntentExtensions ParseQueryIntentExtensions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ModelPackageQueryIntentExtensions();
        }

        return JsonSerializer.Deserialize<ModelPackageQueryIntentExtensions>(json, JsonOptions)
            ?? new ModelPackageQueryIntentExtensions();
    }

    public static Dictionary<string, string> ParseStringDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
