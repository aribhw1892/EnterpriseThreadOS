using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.BusinessPolicies;

public static class BusinessPolicyDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ForbiddenClassificationPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "classificationSchemeVersionId",
        "permissionRules",
        "restrictedContextRules",
        "restrictedContextRuleIds",
        "abacRules",
        "governancePolicyKey"
    };

    public static BusinessPolicyDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<BusinessPolicyCapabilityReferenceResponse> referencedCapabilities,
        IReadOnlyCollection<BusinessPolicyModelPackageReferenceResponse> modelPackages,
        IReadOnlyCollection<BusinessPolicyOntologyReferenceResponse> ontologies)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);
        RejectForbiddenClassificationProperties(payloadJson);

        return new BusinessPolicyDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.PolicyKey!.Trim(),
            document.ConstraintCategory!.Trim(),
            document.ConstraintSummary!.Trim(),
            document.ConstraintRules ?? new Dictionary<string, string>(),
            referencedCapabilities,
            modelPackages,
            ontologies,
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(BusinessPolicyDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static BusinessPolicyDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        RejectForbiddenClassificationProperties(payloadJson);
        var document = JsonSerializer.Deserialize<BusinessPolicyDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Business policy definition payload is invalid.");
        return document;
    }

    public static BusinessPolicyDefinitionPayloadDocument Create(
        string policyKey,
        string constraintCategory,
        string constraintSummary,
        IReadOnlyDictionary<string, string>? constraintRules,
        IReadOnlyCollection<Guid>? referencedCapabilityDefinitionVersionIds,
        IReadOnlyCollection<Guid>? compatibleModelPackageVersionIds,
        IReadOnlyCollection<Guid>? compatibleOntologyVersionIds,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new BusinessPolicyDefinitionPayloadDocument
        {
            PolicyKey = policyKey.Trim(),
            ConstraintCategory = constraintCategory.Trim(),
            ConstraintSummary = constraintSummary.Trim(),
            ConstraintRules = constraintRules?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            ReferencedCapabilityDefinitionVersionIds = referencedCapabilityDefinitionVersionIds?.Distinct().ToList() ?? [],
            CompatibleModelPackageVersionIds = compatibleModelPackageVersionIds?.Distinct().ToList() ?? [],
            CompatibleOntologyVersionIds = compatibleOntologyVersionIds?.Distinct().ToList() ?? [],
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(BusinessPolicyDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.PolicyKey))
        {
            throw new RequestValidationException("policyKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ConstraintCategory))
        {
            throw new RequestValidationException("constraintCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ConstraintSummary))
        {
            throw new RequestValidationException("constraintSummary is required.");
        }

        var capabilityCount = document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0;
        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (capabilityCount + packageCount + ontologyCount == 0)
        {
            throw new RequestValidationException(
                "At least one referencedCapabilityDefinitionVersionId, compatibleModelPackageVersionId, or compatibleOntologyVersionId is required.");
        }

        if ((document.ConstraintRules?.Count ?? 0) == 0)
        {
            throw new RequestValidationException("At least one constraintRules entry is required.");
        }
    }

    private static void RejectForbiddenClassificationProperties(string payloadJson)
    {
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (ForbiddenClassificationPropertyNames.Contains(property.Name))
            {
                throw new RequestValidationException(
                    $"Property '{property.Name}' is reserved for classification governance policies and is not allowed on business policy definitions.");
            }
        }
    }

    private static BusinessPolicyDefinitionPayloadDocument Normalize(BusinessPolicyDefinitionPayloadDocument document)
    {
        document.PolicyKey = document.PolicyKey?.Trim() ?? string.Empty;
        document.ConstraintCategory = document.ConstraintCategory?.Trim() ?? string.Empty;
        document.ConstraintSummary = document.ConstraintSummary?.Trim() ?? string.Empty;
        document.ConstraintRules ??= new Dictionary<string, string>();
        document.ReferencedCapabilityDefinitionVersionIds ??= [];
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    public sealed class BusinessPolicyDefinitionPayloadDocument
    {
        public string? PolicyKey { get; set; }
        public string? ConstraintCategory { get; set; }
        public string? ConstraintSummary { get; set; }
        public Dictionary<string, string>? ConstraintRules { get; set; }
        public List<Guid>? ReferencedCapabilityDefinitionVersionIds { get; set; }
        public List<Guid>? CompatibleModelPackageVersionIds { get; set; }
        public List<Guid>? CompatibleOntologyVersionIds { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
