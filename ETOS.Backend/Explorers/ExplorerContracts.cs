using System.Text.Json.Serialization;

namespace ETOS.Backend.Explorers;

public static class ExplorerPermissions
{
    public const string Read = "explorers.read";
    public const string ContextView = "context_view.read";
    public const string GovernanceFlow = "governance_flow.read";
    public const string GraphExplorer = "graph.explorer.read";
    public const string Admin = "explorers.admin";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContextViewAnchorKind
{
    Artifact = 0,
    Document = 1,
    GraphNode = 2,
    ContextPackage = 3,
    AiTrace = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContextViewSectionVisibility
{
    Visible = 0,
    Denied = 1,
    Empty = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GovernanceFlowNodeKind
{
    Anchor = 0,
    Artifact = 1,
    ArtifactVersion = 2,
    Document = 3,
    GraphNode = 4,
    AiTrace = 5,
    AuditRecord = 6,
    Dependency = 7,
    PlaceholderReviewChain = 8
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GovernanceFlowEdgeKind
{
    Relationship = 0,
    Dependency = 1,
    TraceLink = 2,
    AuditLink = 3,
    EvidenceLink = 4,
    PlaceholderChain = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GovernanceFlowPlaceholderKind
{
    Recommendation = 0,
    ReviewTask = 1,
    Decision = 2,
    OutcomeCheck = 3,
    LearningSignal = 4
}

public sealed record ContextViewItemResponse(
    string ItemId,
    string ItemType,
    string Title,
    string SafeSummary,
    string? LinkRoute,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record ContextViewSectionResponse(
    string SectionKey,
    string Title,
    ContextViewSectionVisibility Visibility,
    string? DeniedReason,
    IReadOnlyCollection<ContextViewItemResponse> Items,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record ContextViewFilterSummaryResponse(
    int VisibleSectionCount,
    int DeniedSectionCount,
    int EmptySectionCount,
    int PolicyDeniedCount,
    string? PolicyKey);

public sealed record ContextView360Response(
    ContextViewAnchorKind AnchorKind,
    string AnchorId,
    string Title,
    string SafeSummary,
    IReadOnlyCollection<ContextViewSectionResponse> Sections,
    GovernanceFlowResponse? GovernanceFlow,
    ContextViewFilterSummaryResponse FilterSummary);

public sealed record GovernanceFlowNodeResponse(
    string NodeId,
    GovernanceFlowNodeKind Kind,
    string Title,
    string SafeSummary,
    string Status,
    string? LinkRoute);

public sealed record GovernanceFlowEdgeResponse(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    GovernanceFlowEdgeKind Kind,
    string Label);

public sealed record GovernanceFlowPlaceholderResponse(
    GovernanceFlowPlaceholderKind Kind,
    string Title,
    string Status,
    string PrdReference,
    string SafeSummary);

public sealed record GovernanceFlowResponse(
    IReadOnlyCollection<GovernanceFlowNodeResponse> Nodes,
    IReadOnlyCollection<GovernanceFlowEdgeResponse> Edges,
    IReadOnlyCollection<GovernanceFlowPlaceholderResponse> FutureChainPlaceholders);

public sealed record GraphExplorerNodeSummaryResponse(
    Guid NodeId,
    string ObjectType,
    string TrustState,
    string GraphSpace,
    string SafeSummary,
    string? SourceBatchId,
    IReadOnlyDictionary<string, string> AllowedAttributes);

public sealed record GraphExplorerNodeDetailResponse(
    Guid NodeId,
    string ObjectType,
    string TrustState,
    string GraphSpace,
    string SafeSummary,
    string? SourceBatchId,
    IReadOnlyDictionary<string, string> AllowedAttributes,
    string ContextViewRoute,
    string ChatRoute);

public sealed record GraphExplorerRelationshipResponse(
    Guid RelationshipId,
    string RelationshipType,
    string Direction,
    Guid AdjacentNodeId,
    string AdjacentObjectType,
    string TrustState,
    string SafeSummary);

public sealed record ContextPackageExplorerSummaryResponse(
    Guid PackageId,
    Guid RetrievalRunId,
    string IntentKey,
    string StrategyKey,
    int RetrievedCount,
    int FilteredCount,
    int DeniedCount,
    string SafeSummary,
    DateTimeOffset CreatedAt,
    Guid? AiTraceRecordId);

public sealed record ContextPackageExplorerDetailResponse(
    Guid PackageId,
    Guid RetrievalRunId,
    string IntentKey,
    string StrategyKey,
    int AllowedCount,
    int DeniedCount,
    string SafeSummary,
    Guid? AiTraceRecordId,
    string? TraceRoute,
    IReadOnlyCollection<string> DeniedSummarySamples);

public sealed record DecisionExplorerItemResponse(
    Guid ArtifactId,
    string ArtifactType,
    string Title,
    string Status,
    IReadOnlyCollection<string> ParticipantUserIds,
    int EvidenceCount,
    string ConflictState,
    string OutcomeSummary,
    string ContextViewRoute);

public sealed record ArtifactExplorerSummaryResponse(
    Guid Id,
    string ArtifactType,
    string Name,
    string LifecycleState,
    string? LatestVersionLabel,
    string SafeSummary,
    string ContextViewRoute,
    DateTimeOffset UpdatedAt);
