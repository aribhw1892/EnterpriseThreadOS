namespace ETOS.Backend.ToolRegistry;

public sealed record ToolCapabilityFlagsResponse(
    bool ReadOnly,
    bool CreatesPlatformArtifact,
    bool CreatesReviewTask,
    bool CreatesDecision,
    bool CallsExternalSystem,
    bool WritesExternalSystem,
    bool RequiresApproval,
    bool SupportsDryRun);

public sealed record ToolModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record ToolOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record ToolCapabilityReferenceResponse(
    Guid CapabilityDefinitionVersionId,
    Guid CapabilityArtifactId,
    string CapabilityArtifactName,
    string CapabilityKey,
    string VersionLabel,
    string ReadinessState);

public sealed record ToolBusinessPolicyReferenceResponse(
    Guid BusinessPolicyDefinitionVersionId,
    Guid BusinessPolicyArtifactId,
    string BusinessPolicyArtifactName,
    string PolicyKey,
    string VersionLabel,
    string ReadinessState);

public sealed record ToolOutputSchemaReferenceResponse(
    Guid OutputSchemaVersionId,
    Guid OutputSchemaArtifactId,
    string OutputSchemaArtifactName,
    string VersionLabel,
    string ReadinessState);

public sealed record ToolConnectorReferenceResponse(
    Guid ConnectorDefinitionVersionId,
    Guid ConnectorArtifactId,
    string ConnectorArtifactName,
    string ConnectorKey,
    string VersionLabel,
    string ReadinessState);

public sealed record ToolDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? ToolKey,
    string? ToolCategory,
    string? RiskLevel,
    DateTimeOffset UpdatedAt);

public sealed record ToolDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string ToolKey,
    string ToolCategory,
    string RiskLevel,
    ToolCapabilityFlagsResponse CapabilityFlags,
    IReadOnlyCollection<string> RequiredPermissionKeys,
    string InputSchemaJson,
    string OutputSchemaJson,
    string? InternalHandlerKey,
    IReadOnlyCollection<ToolModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<ToolOntologyReferenceResponse> CompatibleOntologies,
    IReadOnlyCollection<ToolCapabilityReferenceResponse> ReferencedCapabilities,
    IReadOnlyCollection<ToolBusinessPolicyReferenceResponse> ReferencedBusinessPolicies,
    ToolOutputSchemaReferenceResponse? ReferencedOutputSchema,
    ToolConnectorReferenceResponse? ReferencedConnector,
    IReadOnlyCollection<string> AllowedQueryIntentKeys,
    IReadOnlyDictionary<string, string> CompositionMetadata,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record ToolDependencySummaryResponse(
    IReadOnlyCollection<ToolModelPackageReferenceResponse> ModelPackages,
    IReadOnlyCollection<ToolOntologyReferenceResponse> Ontologies,
    IReadOnlyCollection<ToolCapabilityReferenceResponse> Capabilities,
    IReadOnlyCollection<ToolBusinessPolicyReferenceResponse> BusinessPolicies,
    ToolOutputSchemaReferenceResponse? OutputSchema,
    ToolConnectorReferenceResponse? Connector);

public sealed record CreateToolDefinitionRequest(
    string Name,
    string? Description,
    string ToolKey,
    string ToolCategory,
    string RiskLevel,
    bool ReadOnly,
    bool CreatesPlatformArtifact,
    bool CreatesReviewTask,
    bool CreatesDecision,
    bool CallsExternalSystem,
    bool WritesExternalSystem,
    bool RequiresApproval,
    bool SupportsDryRun,
    IReadOnlyCollection<string>? RequiredPermissionKeys,
    string InputSchemaJson,
    string OutputSchemaJson,
    string? InternalHandlerKey,
    Guid? ReferencedOutputSchemaVersionId,
    Guid? ConnectorDefinitionVersionId,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<string>? AllowedQueryIntentKeys,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateToolDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string ToolKey,
    string ToolCategory,
    string RiskLevel,
    bool ReadOnly,
    bool CreatesPlatformArtifact,
    bool CreatesReviewTask,
    bool CreatesDecision,
    bool CallsExternalSystem,
    bool WritesExternalSystem,
    bool RequiresApproval,
    bool SupportsDryRun,
    IReadOnlyCollection<string>? RequiredPermissionKeys,
    string InputSchemaJson,
    string OutputSchemaJson,
    string? InternalHandlerKey,
    Guid? ReferencedOutputSchemaVersionId,
    Guid? ConnectorDefinitionVersionId,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    IReadOnlyCollection<Guid>? ReferencedCapabilityDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<string>? AllowedQueryIntentKeys,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateToolDefinitionResponse(Guid ArtifactId, Guid VersionId, string VersionLabel);

public sealed record CreateToolDefinitionVersionResponse(Guid ArtifactId, Guid VersionId, string VersionLabel);

public sealed record MarkToolDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishToolDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);

public sealed record ToolCompatibilityScanResponse(
    Guid ArtifactId,
    Guid VersionId,
    bool IsCompatible,
    IReadOnlyCollection<string> BlockingNotes);

public sealed record ToolExecutionRequest(
    string InputJson,
    Guid? ParentAgentRunId = null,
    Guid? ParentWorkflowRunId = null);

public sealed record ToolExecutionResponse(
    Guid ToolRunId,
    string Status,
    string? OutputSafeSummaryJson,
    Guid? AiTraceRecordId,
    Guid? AuditRecordId,
    IReadOnlyCollection<string> ValidationNotes);
