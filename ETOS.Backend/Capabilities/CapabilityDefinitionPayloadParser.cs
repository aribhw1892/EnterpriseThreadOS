using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Capabilities;

public static class CapabilityDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ForbiddenAgentProfilePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "agentRiskLevel",
        "agentTrustLevel",
        "runtimePermissions",
        "executionScope",
        "toolAllowList",
        "toolDenyList",
        "agentExecutionMode",
        "trustTier"
    };

    public static CapabilityDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<CapabilityModelPackageReferenceResponse> modelPackages,
        IReadOnlyCollection<CapabilityOntologyReferenceResponse> ontologies)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);
        RejectForbiddenAgentProfileProperties(payloadJson);

        return new CapabilityDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.CapabilityKey!.Trim(),
            document.OutcomeCategory!.Trim(),
            document.OutcomeSummary!.Trim(),
            document.OutcomeMetadata ?? new Dictionary<string, string>(),
            modelPackages,
            ontologies,
            document.SuggestedQueryIntentRefs ?? [],
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(CapabilityDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static CapabilityDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        RejectForbiddenAgentProfileProperties(payloadJson);
        var document = JsonSerializer.Deserialize<CapabilityDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Capability definition payload is invalid.");
        return document;
    }

    public static CapabilityDefinitionPayloadDocument Create(
        string capabilityKey,
        string outcomeCategory,
        string outcomeSummary,
        IReadOnlyDictionary<string, string>? outcomeMetadata,
        IReadOnlyCollection<Guid>? compatibleModelPackageVersionIds,
        IReadOnlyCollection<Guid>? compatibleOntologyVersionIds,
        IReadOnlyCollection<string>? suggestedQueryIntentRefs,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new CapabilityDefinitionPayloadDocument
        {
            CapabilityKey = capabilityKey.Trim(),
            OutcomeCategory = outcomeCategory.Trim(),
            OutcomeSummary = outcomeSummary.Trim(),
            OutcomeMetadata = outcomeMetadata?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            CompatibleModelPackageVersionIds = compatibleModelPackageVersionIds?.Distinct().ToList() ?? [],
            CompatibleOntologyVersionIds = compatibleOntologyVersionIds?.Distinct().ToList() ?? [],
            SuggestedQueryIntentRefs = suggestedQueryIntentRefs?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(CapabilityDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.CapabilityKey))
        {
            throw new RequestValidationException("capabilityKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.OutcomeCategory))
        {
            throw new RequestValidationException("outcomeCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.OutcomeSummary))
        {
            throw new RequestValidationException("outcomeSummary is required.");
        }

        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (packageCount + ontologyCount == 0)
        {
            throw new RequestValidationException("At least one compatibleModelPackageVersionId or compatibleOntologyVersionId is required.");
        }
    }

    private static void RejectForbiddenAgentProfileProperties(string payloadJson)
    {
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (ForbiddenAgentProfilePropertyNames.Contains(property.Name))
            {
                throw new RequestValidationException(
                    $"Property '{property.Name}' is reserved for future agent capability profiles and is not allowed on business capability definitions.");
            }
        }
    }

    private static CapabilityDefinitionPayloadDocument Normalize(CapabilityDefinitionPayloadDocument document)
    {
        document.CapabilityKey = document.CapabilityKey?.Trim() ?? string.Empty;
        document.OutcomeCategory = document.OutcomeCategory?.Trim() ?? string.Empty;
        document.OutcomeSummary = document.OutcomeSummary?.Trim() ?? string.Empty;
        document.OutcomeMetadata ??= new Dictionary<string, string>();
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.SuggestedQueryIntentRefs ??= [];
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    public sealed class CapabilityDefinitionPayloadDocument
    {
        public string? CapabilityKey { get; set; }
        public string? OutcomeCategory { get; set; }
        public string? OutcomeSummary { get; set; }
        public Dictionary<string, string>? OutcomeMetadata { get; set; }
        public List<Guid>? CompatibleModelPackageVersionIds { get; set; }
        public List<Guid>? CompatibleOntologyVersionIds { get; set; }
        public List<string>? SuggestedQueryIntentRefs { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
