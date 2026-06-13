using System.Text.Json.Serialization;
using ETOS.Backend.Artifacts;

namespace ETOS.Backend.GovernedChat;

public static class GovernedChatPermissions
{
    public const string Run = "governed_chat.run";
    public const string Draft = "governed_chat.draft";
    public const string Admin = "governed_chat.admin";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatDraftArtifactKind
{
    QueryIntent = 0,
    Dashboard = 1,
    Report = 2
}

public sealed record CreateGovernedChatSessionRequest(
    string? Title,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId);

public sealed record CreateGovernedChatTurnRequest(
    string Message,
    string? IntentKey,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    string? PolicyKey,
    ChatDraftArtifactKind? DraftArtifactKind);

public sealed record GovernedChatSessionSummaryResponse(
    Guid Id,
    Guid TenantId,
    string Title,
    Guid StartedByUserId,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastTurnAt,
    int TurnCount);

public sealed record GovernedChatSessionDetailResponse(
    Guid Id,
    Guid TenantId,
    string Title,
    Guid StartedByUserId,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastTurnAt,
    IReadOnlyCollection<GovernedChatTurnSummaryResponse> Turns);

public sealed record GovernedChatTurnSummaryResponse(
    Guid Id,
    Guid SessionId,
    string UserMessage,
    string AssistantSafeSummary,
    Guid? AiTraceRecordId,
    ChatDraftArtifactKind? DraftArtifactKind,
    DateTimeOffset CreatedAt);

public sealed record GovernedChatEvidenceResponse(
    string ContextId,
    string ContextType,
    string SafeSummary);

public sealed record GovernedChatConfidenceResponse(
    double Overall,
    int RetrievalCount,
    int AllowedCount,
    int DeniedCount,
    int TrustFilteredCount,
    string Notes);

public sealed record GovernedChatDraftArtifactResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ArtifactType,
    string VersionLabel,
    ArtifactReadinessState ReadinessState);

public sealed record GovernedChatTurnResponse(
    Guid TurnId,
    Guid SessionId,
    string AssistantSafeSummary,
    IReadOnlyCollection<GovernedChatEvidenceResponse> Evidence,
    GovernedChatConfidenceResponse Confidence,
    int DeniedSummaryCount,
    Guid AiTraceRecordId,
    Guid RetrievalRunId,
    Guid ContextPackageId,
    GovernedChatDraftArtifactResponse? DraftArtifact);
