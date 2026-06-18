using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.OptimizationModels;

public static class OptimizationModelDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ForbiddenAgentPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "promptTemplate",
        "promptTemplateVersionId",
        "runtimeAdapter",
        "preferredRuntimeAdapterKey",
        "agentKey",
        "templateKey",
        "patternCategory",
        "classificationSchemeVersionId",
        "permissionRules",
        "restrictedContextRules",
        "restrictedContextRuleIds",
        "abacRules",
        "governancePolicyKey"
    };

    public static OptimizationModelDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<OptimizationModelCapabilityReferenceResponse> referencedCapabilities,
        IReadOnlyCollection<OptimizationModelBusinessPolicyReferenceResponse> referencedBusinessPolicies,
        IReadOnlyCollection<OptimizationModelPackageReferenceResponse> modelPackages,
        IReadOnlyCollection<OptimizationModelOntologyReferenceResponse> ontologies)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);
        RejectForbiddenAgentProperties(payloadJson);

        return new OptimizationModelDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.OptimizationKey!.Trim(),
            document.ObjectiveCategory!.Trim(),
            document.ObjectiveSummary!.Trim(),
            document.ObjectiveMetadata ?? new Dictionary<string, string>(),
            document.SolverConfiguration ?? new Dictionary<string, string>(),
            document.InputRequirements ?? [],
            referencedCapabilities,
            referencedBusinessPolicies,
            modelPackages,
            ontologies,
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(OptimizationModelDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static OptimizationModelDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        RejectForbiddenAgentProperties(payloadJson);
        var document = JsonSerializer.Deserialize<OptimizationModelDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Optimization model definition payload is invalid.");
        return document;
    }

    public static OptimizationModelDefinitionPayloadDocument Create(
        string optimizationKey,
        string objectiveCategory,
        string objectiveSummary,
        IReadOnlyDictionary<string, string>? objectiveMetadata,
        IReadOnlyDictionary<string, string>? solverConfiguration,
        IReadOnlyCollection<string>? inputRequirements,
        IReadOnlyCollection<Guid>? referencedCapabilityDefinitionVersionIds,
        IReadOnlyCollection<Guid>? referencedBusinessPolicyDefinitionVersionIds,
        IReadOnlyCollection<Guid>? compatibleModelPackageVersionIds,
        IReadOnlyCollection<Guid>? compatibleOntologyVersionIds,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new OptimizationModelDefinitionPayloadDocument
        {
            OptimizationKey = optimizationKey.Trim(),
            ObjectiveCategory = objectiveCategory.Trim(),
            ObjectiveSummary = objectiveSummary.Trim(),
            ObjectiveMetadata = objectiveMetadata?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            SolverConfiguration = solverConfiguration?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            InputRequirements = inputRequirements?.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
            ReferencedCapabilityDefinitionVersionIds = referencedCapabilityDefinitionVersionIds?.Distinct().ToList() ?? [],
            ReferencedBusinessPolicyDefinitionVersionIds = referencedBusinessPolicyDefinitionVersionIds?.Distinct().ToList() ?? [],
            CompatibleModelPackageVersionIds = compatibleModelPackageVersionIds?.Distinct().ToList() ?? [],
            CompatibleOntologyVersionIds = compatibleOntologyVersionIds?.Distinct().ToList() ?? [],
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(OptimizationModelDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.OptimizationKey))
        {
            throw new RequestValidationException("optimizationKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ObjectiveCategory))
        {
            throw new RequestValidationException("objectiveCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ObjectiveSummary))
        {
            throw new RequestValidationException("objectiveSummary is required.");
        }

        if ((document.InputRequirements?.Count ?? 0) == 0)
        {
            throw new RequestValidationException("At least one inputRequirements entry is required.");
        }

        var capabilityCount = document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0;
        var policyCount = document.ReferencedBusinessPolicyDefinitionVersionIds?.Count ?? 0;
        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (capabilityCount + policyCount + packageCount + ontologyCount == 0)
        {
            throw new RequestValidationException(
                "At least one referencedCapabilityDefinitionVersionId, referencedBusinessPolicyDefinitionVersionId, compatibleModelPackageVersionId, or compatibleOntologyVersionId is required.");
        }
    }

    private static void RejectForbiddenAgentProperties(string payloadJson)
    {
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (ForbiddenAgentPropertyNames.Contains(property.Name))
            {
                throw new RequestValidationException(
                    $"Property '{property.Name}' is reserved for agent templates or classification governance and is not allowed on optimization model definitions.");
            }
        }
    }

    private static OptimizationModelDefinitionPayloadDocument Normalize(OptimizationModelDefinitionPayloadDocument document)
    {
        document.OptimizationKey = document.OptimizationKey?.Trim() ?? string.Empty;
        document.ObjectiveCategory = document.ObjectiveCategory?.Trim() ?? string.Empty;
        document.ObjectiveSummary = document.ObjectiveSummary?.Trim() ?? string.Empty;
        document.ObjectiveMetadata ??= new Dictionary<string, string>();
        document.SolverConfiguration ??= new Dictionary<string, string>();
        document.InputRequirements ??= [];
        document.ReferencedCapabilityDefinitionVersionIds ??= [];
        document.ReferencedBusinessPolicyDefinitionVersionIds ??= [];
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    public sealed class OptimizationModelDefinitionPayloadDocument
    {
        public string? OptimizationKey { get; set; }
        public string? ObjectiveCategory { get; set; }
        public string? ObjectiveSummary { get; set; }
        public Dictionary<string, string>? ObjectiveMetadata { get; set; }
        public Dictionary<string, string>? SolverConfiguration { get; set; }
        public List<string>? InputRequirements { get; set; }
        public List<Guid>? ReferencedCapabilityDefinitionVersionIds { get; set; }
        public List<Guid>? ReferencedBusinessPolicyDefinitionVersionIds { get; set; }
        public List<Guid>? CompatibleModelPackageVersionIds { get; set; }
        public List<Guid>? CompatibleOntologyVersionIds { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
