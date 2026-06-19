namespace ETOS.Backend.ToolRegistry;

public sealed record ConnectorDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? ConnectorKey,
    string? ConnectorKind,
    bool? ExecutionEnabled,
    DateTimeOffset UpdatedAt);

public sealed record ConnectorDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string ConnectorKey,
    string ConnectorKind,
    bool CallsExternalSystem,
    bool WritesExternalSystem,
    bool ExecutionEnabled,
    string? DisabledReason,
    string CredentialScopeKey,
    string SecretReferenceKey,
    IReadOnlyCollection<string> SupportedOperations,
    IReadOnlyDictionary<string, string> CompositionMetadata,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record CreateConnectorDefinitionRequest(
    string Name,
    string? Description,
    string ConnectorKey,
    string ConnectorKind,
    bool CallsExternalSystem,
    bool WritesExternalSystem,
    bool ExecutionEnabled,
    string? DisabledReason,
    string CredentialScopeKey,
    string SecretReferenceKey,
    IReadOnlyCollection<string>? SupportedOperations,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateConnectorDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string ConnectorKey,
    string ConnectorKind,
    bool CallsExternalSystem,
    bool WritesExternalSystem,
    bool ExecutionEnabled,
    string? DisabledReason,
    string CredentialScopeKey,
    string SecretReferenceKey,
    IReadOnlyCollection<string>? SupportedOperations,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateConnectorDefinitionResponse(Guid ArtifactId, Guid VersionId, string VersionLabel);

public sealed record CreateConnectorDefinitionVersionResponse(Guid ArtifactId, Guid VersionId, string VersionLabel);

public sealed record MarkConnectorDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishConnectorDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);

public sealed record ScopedCredentialResponse(
    Guid CredentialReferenceId,
    DateTimeOffset ExpiresAt,
    string SafeSummary);
