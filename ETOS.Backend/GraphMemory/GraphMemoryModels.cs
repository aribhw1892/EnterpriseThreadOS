namespace ETOS.Backend.GraphMemory;

public enum GraphSpace
{
    Staging = 0,
    Trusted = 1
}

public enum TrustState
{
    Unverified = 0,
    Provisional = 1,
    Trusted = 2,
    Conflicted = 3
}

public sealed record GraphSourceReference(
    string? SourceSystem,
    string? SourceRecordId,
    string? SourceBatchId);

public sealed record BaseNode(
    Guid NodeId,
    Guid TenantId,
    GraphSpace GraphSpace,
    string ObjectType,
    TrustState TrustState,
    IReadOnlyDictionary<string, string?> Attributes,
    GraphSourceReference? SourceReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BaseRelationship(
    Guid RelationshipId,
    Guid TenantId,
    Guid FromNodeId,
    Guid ToNodeId,
    string RelationshipType,
    TrustState TrustState,
    IReadOnlyDictionary<string, string?> Attributes,
    GraphSourceReference? SourceReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
