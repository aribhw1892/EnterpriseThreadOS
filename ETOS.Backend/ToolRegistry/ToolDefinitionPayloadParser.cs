using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class ToolDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ToolDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        ToolDependencySummaryResponse dependencies)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new ToolDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.ToolKey!.Trim(),
            document.ToolCategory!.Trim(),
            document.RiskLevel!.Trim(),
            new ToolCapabilityFlagsResponse(
                document.ReadOnly,
                document.CreatesPlatformArtifact,
                document.CreatesReviewTask,
                document.CreatesDecision,
                document.CallsExternalSystem,
                document.WritesExternalSystem,
                document.RequiresApproval,
                document.SupportsDryRun),
            document.RequiredPermissionKeys ?? [],
            document.InputSchemaJson!.Trim(),
            document.OutputSchemaJson!.Trim(),
            TrimOptional(document.InternalHandlerKey),
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.OutputSchema,
            dependencies.Connector,
            document.AllowedQueryIntentKeys ?? [],
            document.CompositionMetadata ?? new Dictionary<string, string>(),
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(ToolDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static ToolDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<ToolDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Tool definition payload is invalid.");
        return document;
    }

    public static ToolDefinitionPayloadDocument Create(
        string toolKey,
        string toolCategory,
        string riskLevel,
        bool readOnly,
        bool createsPlatformArtifact,
        bool createsReviewTask,
        bool createsDecision,
        bool callsExternalSystem,
        bool writesExternalSystem,
        bool requiresApproval,
        bool supportsDryRun,
        IReadOnlyCollection<string>? requiredPermissionKeys,
        string inputSchemaJson,
        string outputSchemaJson,
        string? internalHandlerKey,
        Guid? referencedOutputSchemaVersionId,
        Guid? connectorDefinitionVersionId,
        IReadOnlyCollection<Guid>? compatibleModelPackageVersionIds,
        IReadOnlyCollection<Guid>? compatibleOntologyVersionIds,
        IReadOnlyCollection<Guid>? referencedCapabilityDefinitionVersionIds,
        IReadOnlyCollection<Guid>? referencedBusinessPolicyDefinitionVersionIds,
        IReadOnlyCollection<string>? allowedQueryIntentKeys,
        IReadOnlyDictionary<string, string>? compositionMetadata,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new ToolDefinitionPayloadDocument
        {
            ToolKey = toolKey.Trim(),
            ToolCategory = toolCategory.Trim(),
            RiskLevel = riskLevel.Trim(),
            ReadOnly = readOnly,
            CreatesPlatformArtifact = createsPlatformArtifact,
            CreatesReviewTask = createsReviewTask,
            CreatesDecision = createsDecision,
            CallsExternalSystem = callsExternalSystem,
            WritesExternalSystem = writesExternalSystem,
            RequiresApproval = requiresApproval,
            SupportsDryRun = supportsDryRun,
            RequiredPermissionKeys = requiredPermissionKeys?.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
            InputSchemaJson = inputSchemaJson.Trim(),
            OutputSchemaJson = outputSchemaJson.Trim(),
            InternalHandlerKey = TrimOptional(internalHandlerKey),
            ReferencedOutputSchemaVersionId = referencedOutputSchemaVersionId,
            ConnectorDefinitionVersionId = connectorDefinitionVersionId,
            CompatibleModelPackageVersionIds = compatibleModelPackageVersionIds?.Distinct().ToList() ?? [],
            CompatibleOntologyVersionIds = compatibleOntologyVersionIds?.Distinct().ToList() ?? [],
            ReferencedCapabilityDefinitionVersionIds = referencedCapabilityDefinitionVersionIds?.Distinct().ToList() ?? [],
            ReferencedBusinessPolicyDefinitionVersionIds = referencedBusinessPolicyDefinitionVersionIds?.Distinct().ToList() ?? [],
            AllowedQueryIntentKeys = allowedQueryIntentKeys?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            CompositionMetadata = compositionMetadata?.ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(ToolDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.ToolKey))
        {
            throw new RequestValidationException("toolKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ToolCategory))
        {
            throw new RequestValidationException("toolCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.RiskLevel))
        {
            throw new RequestValidationException("riskLevel is required.");
        }

        if (!ToolRiskLevels.All.Contains(document.RiskLevel, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"riskLevel '{document.RiskLevel}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(document.InputSchemaJson))
        {
            throw new RequestValidationException("inputSchemaJson is required.");
        }

        if (string.IsNullOrWhiteSpace(document.OutputSchemaJson))
        {
            throw new RequestValidationException("outputSchemaJson is required.");
        }

        var anchorCount = (document.CompatibleModelPackageVersionIds?.Count ?? 0)
            + (document.CompatibleOntologyVersionIds?.Count ?? 0)
            + (document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0)
            + (document.ReferencedBusinessPolicyDefinitionVersionIds?.Count ?? 0);
        if (anchorCount == 0)
        {
            throw new RequestValidationException("At least one compatibility anchor is required.");
        }

        if (document.CreatesDecision)
        {
            throw new RequestValidationException("Tools cannot create decision artifacts in MVP.");
        }
    }

    private static ToolDefinitionPayloadDocument Normalize(ToolDefinitionPayloadDocument document)
    {
        document.ToolKey = document.ToolKey?.Trim() ?? string.Empty;
        document.ToolCategory = document.ToolCategory?.Trim() ?? string.Empty;
        document.RiskLevel = document.RiskLevel?.Trim() ?? string.Empty;
        document.InputSchemaJson = document.InputSchemaJson?.Trim() ?? string.Empty;
        document.OutputSchemaJson = document.OutputSchemaJson?.Trim() ?? string.Empty;
        document.InternalHandlerKey = TrimOptional(document.InternalHandlerKey);
        document.RequiredPermissionKeys ??= [];
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.ReferencedCapabilityDefinitionVersionIds ??= [];
        document.ReferencedBusinessPolicyDefinitionVersionIds ??= [];
        document.AllowedQueryIntentKeys ??= [];
        document.CompositionMetadata ??= new Dictionary<string, string>();
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class ToolDefinitionPayloadDocument
    {
        public string? ToolKey { get; set; }
        public string? ToolCategory { get; set; }
        public string? RiskLevel { get; set; }
        public bool ReadOnly { get; set; } = true;
        public bool CreatesPlatformArtifact { get; set; }
        public bool CreatesReviewTask { get; set; }
        public bool CreatesDecision { get; set; }
        public bool CallsExternalSystem { get; set; }
        public bool WritesExternalSystem { get; set; }
        public bool RequiresApproval { get; set; }
        public bool SupportsDryRun { get; set; } = true;
        public List<string>? RequiredPermissionKeys { get; set; }
        public string? InputSchemaJson { get; set; }
        public string? OutputSchemaJson { get; set; }
        public string? InternalHandlerKey { get; set; }
        public Guid? ReferencedOutputSchemaVersionId { get; set; }
        public Guid? ConnectorDefinitionVersionId { get; set; }
        public List<Guid>? CompatibleModelPackageVersionIds { get; set; }
        public List<Guid>? CompatibleOntologyVersionIds { get; set; }
        public List<Guid>? ReferencedCapabilityDefinitionVersionIds { get; set; }
        public List<Guid>? ReferencedBusinessPolicyDefinitionVersionIds { get; set; }
        public List<string>? AllowedQueryIntentKeys { get; set; }
        public Dictionary<string, string>? CompositionMetadata { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
