using System.Text.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentTemplates;

public static class AgentTemplateDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ForbiddenOptimizationSolverPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "optimizationKey",
        "objectiveCategory",
        "objectiveSummary",
        "solverConfiguration",
        "inputRequirements",
        "objectiveMetadata"
    };

    public static AgentTemplateDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<AgentTemplateCapabilityReferenceResponse> referencedCapabilities,
        IReadOnlyCollection<AgentTemplateBusinessPolicyReferenceResponse> referencedBusinessPolicies,
        IReadOnlyCollection<AgentTemplateOptimizationModelReferenceResponse> referencedOptimizationModels,
        IReadOnlyCollection<AgentTemplateModelPackageReferenceResponse> modelPackages,
        IReadOnlyCollection<AgentTemplateOntologyReferenceResponse> ontologies,
        AgentTemplateArtifactVersionReferenceResponse? promptTemplate,
        AgentTemplateArtifactVersionReferenceResponse? outputSchema,
        AgentTemplateQueryIntentReferenceResponse? queryIntent,
        AgentTemplateRetrievalStrategyReferenceResponse? retrievalStrategy,
        IReadOnlyCollection<AgentTemplateToolReferenceResponse> referencedTools)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);
        RejectForbiddenOptimizationSolverProperties(payloadJson);

        return new AgentTemplateDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.TemplateKey!.Trim(),
            document.PatternCategory!.Trim(),
            document.PatternSummary!.Trim(),
            document.PreferredRuntimeAdapterKey!.Trim(),
            modelPackages,
            ontologies,
            referencedCapabilities,
            referencedBusinessPolicies,
            referencedOptimizationModels,
            promptTemplate,
            outputSchema,
            queryIntent,
            retrievalStrategy,
            referencedTools,
            document.CompositionMetadata ?? new Dictionary<string, string>(),
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(AgentTemplateDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static AgentTemplateDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        RejectForbiddenOptimizationSolverProperties(payloadJson);
        var document = JsonSerializer.Deserialize<AgentTemplateDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Agent template definition payload is invalid.");
        return document;
    }

    public static AgentTemplateDefinitionPayloadDocument Create(
        string templateKey,
        string patternCategory,
        string patternSummary,
        string? preferredRuntimeAdapterKey,
        IReadOnlyCollection<Guid>? compatibleModelPackageVersionIds,
        IReadOnlyCollection<Guid>? compatibleOntologyVersionIds,
        IReadOnlyCollection<Guid>? referencedCapabilityDefinitionVersionIds,
        IReadOnlyCollection<Guid>? referencedBusinessPolicyDefinitionVersionIds,
        IReadOnlyCollection<Guid>? referencedOptimizationModelVersionIds,
        Guid? promptTemplateVersionId,
        Guid? outputSchemaVersionId,
        Guid? queryIntentVersionId,
        Guid? retrievalStrategyVersionId,
        IReadOnlyCollection<Guid>? referencedToolDefinitionVersionIds,
        IReadOnlyDictionary<string, string>? compositionMetadata,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new AgentTemplateDefinitionPayloadDocument
        {
            TemplateKey = templateKey.Trim(),
            PatternCategory = patternCategory.Trim(),
            PatternSummary = patternSummary.Trim(),
            PreferredRuntimeAdapterKey = string.IsNullOrWhiteSpace(preferredRuntimeAdapterKey)
                ? AgentRuntimeAdapterKeys.PydanticAi
                : preferredRuntimeAdapterKey.Trim(),
            CompatibleModelPackageVersionIds = compatibleModelPackageVersionIds?.Distinct().ToList() ?? [],
            CompatibleOntologyVersionIds = compatibleOntologyVersionIds?.Distinct().ToList() ?? [],
            ReferencedCapabilityDefinitionVersionIds = referencedCapabilityDefinitionVersionIds?.Distinct().ToList() ?? [],
            ReferencedBusinessPolicyDefinitionVersionIds = referencedBusinessPolicyDefinitionVersionIds?.Distinct().ToList() ?? [],
            ReferencedOptimizationModelVersionIds = referencedOptimizationModelVersionIds?.Distinct().ToList() ?? [],
            PromptTemplateVersionId = promptTemplateVersionId,
            OutputSchemaVersionId = outputSchemaVersionId,
            QueryIntentVersionId = queryIntentVersionId,
            RetrievalStrategyVersionId = retrievalStrategyVersionId,
            ReferencedToolDefinitionVersionIds = referencedToolDefinitionVersionIds?.Distinct().ToList() ?? [],
            CompositionMetadata = compositionMetadata?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(AgentTemplateDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.TemplateKey))
        {
            throw new RequestValidationException("templateKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PatternCategory))
        {
            throw new RequestValidationException("patternCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PatternSummary))
        {
            throw new RequestValidationException("patternSummary is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PreferredRuntimeAdapterKey))
        {
            throw new RequestValidationException("preferredRuntimeAdapterKey is required.");
        }

        if (!AgentRuntimeAdapterKeys.All.Contains(document.PreferredRuntimeAdapterKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"preferredRuntimeAdapterKey '{document.PreferredRuntimeAdapterKey}' is not a known adapter key.");
        }

        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        var capabilityCount = document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0;

        if (packageCount + ontologyCount == 0)
        {
            throw new RequestValidationException(
                "At least one compatibleModelPackageVersionId or compatibleOntologyVersionId is required.");
        }

        if (capabilityCount == 0)
        {
            throw new RequestValidationException("At least one referencedCapabilityDefinitionVersionId is required.");
        }

        if (document.PromptTemplateVersionId is null || document.PromptTemplateVersionId == Guid.Empty)
        {
            throw new RequestValidationException("promptTemplateVersionId is required.");
        }

        if (document.OutputSchemaVersionId is null || document.OutputSchemaVersionId == Guid.Empty)
        {
            throw new RequestValidationException("outputSchemaVersionId is required.");
        }

        if (document.QueryIntentVersionId is null || document.QueryIntentVersionId == Guid.Empty)
        {
            throw new RequestValidationException("queryIntentVersionId is required.");
        }

        if (document.RetrievalStrategyVersionId is null || document.RetrievalStrategyVersionId == Guid.Empty)
        {
            throw new RequestValidationException("retrievalStrategyVersionId is required.");
        }
    }

    private static void RejectForbiddenOptimizationSolverProperties(string payloadJson)
    {
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (ForbiddenOptimizationSolverPropertyNames.Contains(property.Name))
            {
                throw new RequestValidationException(
                    $"Property '{property.Name}' is reserved for optimization model definitions and is not allowed on agent template definitions.");
            }
        }
    }

    private static AgentTemplateDefinitionPayloadDocument Normalize(AgentTemplateDefinitionPayloadDocument document)
    {
        document.TemplateKey = document.TemplateKey?.Trim() ?? string.Empty;
        document.PatternCategory = document.PatternCategory?.Trim() ?? string.Empty;
        document.PatternSummary = document.PatternSummary?.Trim() ?? string.Empty;
        document.PreferredRuntimeAdapterKey = string.IsNullOrWhiteSpace(document.PreferredRuntimeAdapterKey)
            ? AgentRuntimeAdapterKeys.PydanticAi
            : document.PreferredRuntimeAdapterKey.Trim();
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.ReferencedCapabilityDefinitionVersionIds ??= [];
        document.ReferencedBusinessPolicyDefinitionVersionIds ??= [];
        document.ReferencedOptimizationModelVersionIds ??= [];
        document.ReferencedToolDefinitionVersionIds ??= [];
        document.CompositionMetadata ??= new Dictionary<string, string>();
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    public sealed class AgentTemplateDefinitionPayloadDocument
    {
        public string? TemplateKey { get; set; }
        public string? PatternCategory { get; set; }
        public string? PatternSummary { get; set; }
        public string? PreferredRuntimeAdapterKey { get; set; }
        public List<Guid>? CompatibleModelPackageVersionIds { get; set; }
        public List<Guid>? CompatibleOntologyVersionIds { get; set; }
        public List<Guid>? ReferencedCapabilityDefinitionVersionIds { get; set; }
        public List<Guid>? ReferencedBusinessPolicyDefinitionVersionIds { get; set; }
        public List<Guid>? ReferencedOptimizationModelVersionIds { get; set; }
        public Guid? PromptTemplateVersionId { get; set; }
        public Guid? OutputSchemaVersionId { get; set; }
        public Guid? QueryIntentVersionId { get; set; }
        public Guid? RetrievalStrategyVersionId { get; set; }
        public List<Guid>? ReferencedToolDefinitionVersionIds { get; set; }
        public Dictionary<string, string>? CompositionMetadata { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
