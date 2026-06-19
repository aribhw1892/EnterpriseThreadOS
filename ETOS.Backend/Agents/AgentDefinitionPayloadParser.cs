using System.Text.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Agents;

public static class AgentDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        AgentTypeReferenceResponse? agentType,
        IReadOnlyCollection<AgentCapabilityReferenceResponse> referencedCapabilities,
        IReadOnlyCollection<AgentBusinessPolicyReferenceResponse> referencedBusinessPolicies,
        IReadOnlyCollection<AgentOptimizationModelReferenceResponse> referencedOptimizationModels,
        IReadOnlyCollection<AgentModelPackageReferenceResponse> modelPackages,
        IReadOnlyCollection<AgentOntologyReferenceResponse> ontologies,
        AgentArtifactVersionReferenceResponse? promptTemplate,
        AgentArtifactVersionReferenceResponse? outputSchema,
        AgentQueryIntentReferenceResponse? queryIntent,
        AgentRetrievalStrategyReferenceResponse? retrievalStrategy,
        IReadOnlyCollection<AgentToolReferenceResponse> referencedTools,
        IReadOnlyCollection<AgentSkillReferenceResponse> referencedSkills)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new AgentDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.AgentKey!.Trim(),
            document.DisplayName!.Trim(),
            TrimOptional(document.Description),
            agentType,
            document.SourceAgentTemplateVersionId,
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
            referencedSkills,
            document.PrimaryModelProviderKey!.Trim(),
            document.PrimaryModelId!.Trim(),
            MapFallbackModels(document.FallbackModels),
            document.SafeModeEnabled,
            document.PreviewModeDefault,
            TrimOptional(document.BlockedModeMessage),
            document.CompatibilityTestNotes ?? [],
            document.CompatibilityFixtureKeys ?? [],
            MapDerivedCapabilityRisk(document.DerivedCapabilityRiskJson),
            document.CreatedByUserId,
            document.CompositionMetadata ?? new Dictionary<string, string>());
    }

    public static string Serialize(AgentDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static AgentDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<AgentDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Agent definition payload is invalid.");
        return document;
    }

    public static AgentDefinitionPayloadDocument Create(
        string agentKey,
        string displayName,
        string? description,
        Guid agentTypeDefinitionVersionId,
        Guid? sourceAgentTemplateVersionId,
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
        IReadOnlyCollection<Guid>? referencedSkillDefinitionVersionIds,
        string primaryModelProviderKey,
        string primaryModelId,
        IReadOnlyCollection<AgentFallbackModelRequest>? fallbackModels,
        bool safeModeEnabled,
        bool previewModeDefault,
        string? blockedModeMessage,
        IReadOnlyCollection<string>? compatibilityTestNotes,
        IReadOnlyCollection<string>? compatibilityFixtureKeys,
        Guid createdByUserId,
        IReadOnlyDictionary<string, string>? compositionMetadata,
        DerivedCapabilityRiskDocument? derivedCapabilityRiskJson = null)
        => Normalize(new AgentDefinitionPayloadDocument
        {
            AgentKey = agentKey.Trim(),
            DisplayName = displayName.Trim(),
            Description = TrimOptional(description),
            AgentTypeDefinitionVersionId = agentTypeDefinitionVersionId,
            SourceAgentTemplateVersionId = sourceAgentTemplateVersionId,
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
            ReferencedSkillDefinitionVersionIds = referencedSkillDefinitionVersionIds?.Distinct().ToList() ?? [],
            PrimaryModelProviderKey = primaryModelProviderKey.Trim(),
            PrimaryModelId = primaryModelId.Trim(),
            FallbackModels = fallbackModels?.Select(item => new FallbackModelDocument
            {
                ProviderKey = item.ProviderKey.Trim(),
                ModelId = item.ModelId.Trim(),
                TriggerReason = item.TriggerReason.Trim()
            }).ToList() ?? [],
            SafeModeEnabled = safeModeEnabled,
            PreviewModeDefault = previewModeDefault,
            BlockedModeMessage = TrimOptional(blockedModeMessage),
            CompatibilityTestNotes = compatibilityTestNotes?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            CompatibilityFixtureKeys = compatibilityFixtureKeys?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            DerivedCapabilityRiskJson = derivedCapabilityRiskJson,
            CreatedByUserId = createdByUserId,
            CompositionMetadata = compositionMetadata?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>()
        });

    public static void ValidateCore(AgentDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.AgentKey))
        {
            throw new RequestValidationException("agentKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.DisplayName))
        {
            throw new RequestValidationException("displayName is required.");
        }

        if (document.AgentTypeDefinitionVersionId == Guid.Empty)
        {
            throw new RequestValidationException("agentTypeDefinitionVersionId is required.");
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

        if (string.IsNullOrWhiteSpace(document.PrimaryModelProviderKey))
        {
            throw new RequestValidationException("primaryModelProviderKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PrimaryModelId))
        {
            throw new RequestValidationException("primaryModelId is required.");
        }

        if (document.CreatedByUserId == Guid.Empty)
        {
            throw new RequestValidationException("createdByUserId is required.");
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

    public static AgentDerivedCapabilityRiskResponse? MapDerivedCapabilityRisk(DerivedCapabilityRiskDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return new AgentDerivedCapabilityRiskResponse(
            document.EffectiveRiskLevel?.Trim() ?? string.Empty,
            document.ToolRiskContributions?.Select(item => new AgentToolRiskContributionResponse(
                item.ToolDefinitionVersionId,
                item.RiskLevel?.Trim() ?? string.Empty)).ToList() ?? [],
            new AgentRetrievalRiskResponse(
                document.RetrievalRisk?.AllowsSemanticFallback ?? false,
                document.RetrievalRisk?.AllowsVectorFallback ?? false),
            document.PermissionCeiling?.Trim() ?? string.Empty);
    }

    private static IReadOnlyCollection<AgentFallbackModelResponse> MapFallbackModels(IReadOnlyCollection<FallbackModelDocument>? models)
        => models?.Select(item => new AgentFallbackModelResponse(
            item.ProviderKey?.Trim() ?? string.Empty,
            item.ModelId?.Trim() ?? string.Empty,
            item.TriggerReason?.Trim() ?? string.Empty)).ToList() ?? [];

    private static AgentDefinitionPayloadDocument Normalize(AgentDefinitionPayloadDocument document)
    {
        document.AgentKey = document.AgentKey?.Trim() ?? string.Empty;
        document.DisplayName = document.DisplayName?.Trim() ?? string.Empty;
        document.Description = TrimOptional(document.Description);
        document.PreferredRuntimeAdapterKey = string.IsNullOrWhiteSpace(document.PreferredRuntimeAdapterKey)
            ? AgentRuntimeAdapterKeys.PydanticAi
            : document.PreferredRuntimeAdapterKey.Trim();
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.ReferencedCapabilityDefinitionVersionIds ??= [];
        document.ReferencedBusinessPolicyDefinitionVersionIds ??= [];
        document.ReferencedOptimizationModelVersionIds ??= [];
        document.ReferencedToolDefinitionVersionIds ??= [];
        document.ReferencedSkillDefinitionVersionIds ??= [];
        document.PrimaryModelProviderKey = document.PrimaryModelProviderKey?.Trim() ?? string.Empty;
        document.PrimaryModelId = document.PrimaryModelId?.Trim() ?? string.Empty;
        document.FallbackModels ??= [];
        document.BlockedModeMessage = TrimOptional(document.BlockedModeMessage);
        document.CompatibilityTestNotes ??= [];
        document.CompatibilityFixtureKeys ??= [];
        document.CompositionMetadata ??= new Dictionary<string, string>();
        return document;
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class AgentDefinitionPayloadDocument
    {
        public string? AgentKey { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public Guid AgentTypeDefinitionVersionId { get; set; }
        public Guid? SourceAgentTemplateVersionId { get; set; }
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
        public List<Guid>? ReferencedSkillDefinitionVersionIds { get; set; }
        public string? PrimaryModelProviderKey { get; set; }
        public string? PrimaryModelId { get; set; }
        public List<FallbackModelDocument>? FallbackModels { get; set; }
        public bool SafeModeEnabled { get; set; }
        public bool PreviewModeDefault { get; set; }
        public string? BlockedModeMessage { get; set; }
        public List<string>? CompatibilityTestNotes { get; set; }
        public List<string>? CompatibilityFixtureKeys { get; set; }
        public DerivedCapabilityRiskDocument? DerivedCapabilityRiskJson { get; set; }
        public Guid CreatedByUserId { get; set; }
        public Dictionary<string, string>? CompositionMetadata { get; set; }
    }

    public sealed class FallbackModelDocument
    {
        public string? ProviderKey { get; set; }
        public string? ModelId { get; set; }
        public string? TriggerReason { get; set; }
    }

    public sealed class DerivedCapabilityRiskDocument
    {
        public string? EffectiveRiskLevel { get; set; }
        public List<ToolRiskContributionDocument>? ToolRiskContributions { get; set; }
        public RetrievalRiskDocument? RetrievalRisk { get; set; }
        public string? PermissionCeiling { get; set; }
    }

    public sealed class ToolRiskContributionDocument
    {
        public Guid ToolDefinitionVersionId { get; set; }
        public string? RiskLevel { get; set; }
    }

    public sealed class RetrievalRiskDocument
    {
        public bool AllowsSemanticFallback { get; set; }
        public bool AllowsVectorFallback { get; set; }
    }
}
