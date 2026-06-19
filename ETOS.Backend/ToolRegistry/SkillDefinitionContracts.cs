namespace ETOS.Backend.ToolRegistry;

public sealed record SkillDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? SkillKey,
    bool IsGloballyShared,
    DateTimeOffset UpdatedAt);

public sealed record SkillToolReferenceResponse(
    Guid ToolDefinitionVersionId,
    Guid ToolArtifactId,
    string ToolArtifactName,
    string ToolKey,
    string VersionLabel,
    string ReadinessState);

public sealed record SkillDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string SkillKey,
    string SkillSummary,
    bool IsGloballyShared,
    string InputSchemaJson,
    string OutputSchemaJson,
    IReadOnlyCollection<SkillToolReferenceResponse> ReferencedTools,
    IReadOnlyDictionary<string, string> CompositionMetadata,
    IReadOnlyCollection<string> FutureExtensionPlaceholders);

public sealed record SkillDependencySummaryResponse(
    IReadOnlyCollection<SkillToolReferenceResponse> Tools);

public sealed record CreateSkillDefinitionRequest(
    string Name,
    string? Description,
    string SkillKey,
    string SkillSummary,
    bool IsGloballyShared,
    string InputSchemaJson,
    string OutputSchemaJson,
    IReadOnlyCollection<Guid>? ReferencedToolDefinitionVersionIds,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateSkillDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string SkillKey,
    string SkillSummary,
    bool IsGloballyShared,
    string InputSchemaJson,
    string OutputSchemaJson,
    IReadOnlyCollection<Guid>? ReferencedToolDefinitionVersionIds,
    IReadOnlyDictionary<string, string>? CompositionMetadata,
    IReadOnlyCollection<string>? FutureExtensionPlaceholders);

public sealed record CreateSkillDefinitionResponse(Guid ArtifactId, Guid VersionId, string VersionLabel);

public sealed record CreateSkillDefinitionVersionResponse(Guid ArtifactId, Guid VersionId, string VersionLabel);

public sealed record MarkSkillDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishSkillDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
