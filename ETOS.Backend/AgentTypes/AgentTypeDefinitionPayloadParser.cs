using System.Text.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.ToolRegistry;

namespace ETOS.Backend.AgentTypes;

public static class AgentTypeDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentTypeDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new AgentTypeDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.TypeKey!.Trim(),
            document.Purpose!.Trim(),
            document.AllowedIntentCategoryKeys ?? [],
            document.DefaultPatternCategory!.Trim(),
            document.RiskBaseline!.Trim());
    }

    public static string Serialize(AgentTypeDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static AgentTypeDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<AgentTypeDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Agent type definition payload is invalid.");
        return document;
    }

    public static AgentTypeDefinitionPayloadDocument Create(
        string typeKey,
        string purpose,
        IReadOnlyCollection<string>? allowedIntentCategoryKeys,
        string defaultPatternCategory,
        string riskBaseline)
        => Normalize(new AgentTypeDefinitionPayloadDocument
        {
            TypeKey = typeKey.Trim(),
            Purpose = purpose.Trim(),
            AllowedIntentCategoryKeys = allowedIntentCategoryKeys?.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
            DefaultPatternCategory = defaultPatternCategory.Trim(),
            RiskBaseline = riskBaseline.Trim()
        });

    public static void ValidateCore(AgentTypeDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.TypeKey))
        {
            throw new RequestValidationException("typeKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.Purpose))
        {
            throw new RequestValidationException("purpose is required.");
        }

        if (string.IsNullOrWhiteSpace(document.DefaultPatternCategory))
        {
            throw new RequestValidationException("defaultPatternCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.RiskBaseline))
        {
            throw new RequestValidationException("riskBaseline is required.");
        }

        if (!ToolRiskLevels.All.Contains(document.RiskBaseline, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"riskBaseline '{document.RiskBaseline}' is not supported.");
        }
    }

    private static AgentTypeDefinitionPayloadDocument Normalize(AgentTypeDefinitionPayloadDocument document)
    {
        document.TypeKey = document.TypeKey?.Trim() ?? string.Empty;
        document.Purpose = document.Purpose?.Trim() ?? string.Empty;
        document.AllowedIntentCategoryKeys ??= [];
        document.DefaultPatternCategory = document.DefaultPatternCategory?.Trim() ?? string.Empty;
        document.RiskBaseline = document.RiskBaseline?.Trim() ?? string.Empty;
        return document;
    }

    public sealed class AgentTypeDefinitionPayloadDocument
    {
        public string? TypeKey { get; set; }
        public string? Purpose { get; set; }
        public List<string>? AllowedIntentCategoryKeys { get; set; }
        public string? DefaultPatternCategory { get; set; }
        public string? RiskBaseline { get; set; }
    }
}
