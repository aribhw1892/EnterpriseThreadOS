namespace ETOS.Backend.GraphMemory;

public sealed record CreateGraphNodeRequest(
    Guid TenantId,
    GraphSpace GraphSpace,
    string ObjectType,
    TrustState TrustState,
    IReadOnlyDictionary<string, string?>? Attributes,
    GraphSourceReference? SourceReference);

public sealed record UpdateGraphNodeRequest(
    Guid TenantId,
    Guid NodeId,
    TrustState? TrustState,
    IReadOnlyDictionary<string, string?>? Attributes,
    GraphSourceReference? SourceReference);

public sealed record CreateGraphRelationshipRequest(
    Guid TenantId,
    Guid FromNodeId,
    Guid ToNodeId,
    string RelationshipType,
    TrustState TrustState,
    IReadOnlyDictionary<string, string?>? Attributes,
    GraphSourceReference? SourceReference);

public sealed record TraverseGraphRequest(
    Guid TenantId,
    Guid StartNodeId,
    GraphSpace? GraphSpace,
    int MaxDepth,
    IReadOnlyCollection<string>? RelationshipTypes,
    IReadOnlyCollection<TrustState>? AllowedTrustStates);

public sealed record GraphTraversalResult(
    BaseNode StartNode,
    IReadOnlyCollection<BaseNode> Nodes,
    IReadOnlyCollection<BaseRelationship> Relationships);

public sealed record GraphReadModel(
    IReadOnlyCollection<BaseNode> Nodes,
    IReadOnlyCollection<BaseRelationship> Relationships);

public sealed record GraphPromotionCopyResult(
    IReadOnlyCollection<Guid> TrustedNodeIds,
    IReadOnlyCollection<Guid> TrustedRelationshipIds);

public sealed record GraphHealthResponse(
    string Provider,
    string Status,
    string Description,
    bool BootstrapApplied,
    long DurationMilliseconds);

public sealed record GraphSnapshotContract(
    Guid SnapshotId,
    Guid TenantId,
    GraphSpace GraphSpace,
    DateTimeOffset CapturedAt,
    int NodeCount,
    int RelationshipCount,
    string ChecksumSha256,
    string SafeSummary);

public sealed record GraphDiffContract(
    Guid DiffId,
    Guid TenantId,
    Guid FromSnapshotId,
    Guid ToSnapshotId,
    DateTimeOffset CreatedAt,
    string ChecksumSha256,
    string SafeSummary);
