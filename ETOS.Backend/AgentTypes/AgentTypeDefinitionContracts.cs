namespace ETOS.Backend.AgentTypes;

public static class AgentTypeDefinitionPermissions
{
    public const string Read = "agent-types.read";
    public const string Create = "agent-types.create";
    public const string Readiness = "agent-types.readiness";
    public const string Admin = "agent-types.admin";
}

public static class AgentTypeDefinitionArtifactTypes
{
    public const string AgentTypeDefinition = "AgentTypeDefinition";
}

public sealed record AgentTypeDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? TypeKey,
    string? DefaultPatternCategory,
    string? RiskBaseline,
    DateTimeOffset UpdatedAt);

public sealed record AgentTypeDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string TypeKey,
    string Purpose,
    IReadOnlyCollection<string> AllowedIntentCategoryKeys,
    string DefaultPatternCategory,
    string RiskBaseline);

public sealed record CreateAgentTypeDefinitionRequest(
    string Name,
    string? Description,
    string TypeKey,
    string Purpose,
    IReadOnlyCollection<string>? AllowedIntentCategoryKeys,
    string DefaultPatternCategory,
    string RiskBaseline);

public sealed record CreateAgentTypeDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string TypeKey,
    string Purpose,
    IReadOnlyCollection<string>? AllowedIntentCategoryKeys,
    string DefaultPatternCategory,
    string RiskBaseline);

public sealed record CreateAgentTypeDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateAgentTypeDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkAgentTypeDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishAgentTypeDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
