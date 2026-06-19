using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class SkillDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static SkillDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<SkillToolReferenceResponse> referencedTools)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new SkillDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.SkillKey!.Trim(),
            document.SkillSummary!.Trim(),
            document.IsGloballyShared,
            document.InputSchemaJson!.Trim(),
            document.OutputSchemaJson!.Trim(),
            referencedTools,
            document.CompositionMetadata ?? new Dictionary<string, string>(),
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(SkillDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static SkillDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<SkillDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Skill definition payload is invalid.");
        return document;
    }

    public static SkillDefinitionPayloadDocument Create(
        string skillKey,
        string skillSummary,
        bool isGloballyShared,
        string inputSchemaJson,
        string outputSchemaJson,
        IReadOnlyCollection<Guid>? referencedToolDefinitionVersionIds,
        IReadOnlyDictionary<string, string>? compositionMetadata,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new SkillDefinitionPayloadDocument
        {
            SkillKey = skillKey.Trim(),
            SkillSummary = skillSummary.Trim(),
            IsGloballyShared = isGloballyShared,
            InputSchemaJson = inputSchemaJson.Trim(),
            OutputSchemaJson = outputSchemaJson.Trim(),
            ReferencedToolDefinitionVersionIds = referencedToolDefinitionVersionIds?.Distinct().ToList() ?? [],
            CompositionMetadata = compositionMetadata?.ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(SkillDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.SkillKey))
        {
            throw new RequestValidationException("skillKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.SkillSummary))
        {
            throw new RequestValidationException("skillSummary is required.");
        }

        if (string.IsNullOrWhiteSpace(document.InputSchemaJson))
        {
            throw new RequestValidationException("inputSchemaJson is required.");
        }

        if (string.IsNullOrWhiteSpace(document.OutputSchemaJson))
        {
            throw new RequestValidationException("outputSchemaJson is required.");
        }

        if ((document.ReferencedToolDefinitionVersionIds?.Count ?? 0) == 0)
        {
            throw new RequestValidationException("At least one referencedToolDefinitionVersionId is required.");
        }
    }

    private static SkillDefinitionPayloadDocument Normalize(SkillDefinitionPayloadDocument document)
    {
        document.SkillKey = document.SkillKey?.Trim() ?? string.Empty;
        document.SkillSummary = document.SkillSummary?.Trim() ?? string.Empty;
        document.InputSchemaJson = document.InputSchemaJson?.Trim() ?? string.Empty;
        document.OutputSchemaJson = document.OutputSchemaJson?.Trim() ?? string.Empty;
        document.ReferencedToolDefinitionVersionIds ??= [];
        document.CompositionMetadata ??= new Dictionary<string, string>();
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    public sealed class SkillDefinitionPayloadDocument
    {
        public string? SkillKey { get; set; }
        public string? SkillSummary { get; set; }
        public bool IsGloballyShared { get; set; }
        public string? InputSchemaJson { get; set; }
        public string? OutputSchemaJson { get; set; }
        public List<Guid>? ReferencedToolDefinitionVersionIds { get; set; }
        public Dictionary<string, string>? CompositionMetadata { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
