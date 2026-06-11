using ETOS.Backend.Tenancy;

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

public sealed class GraphSnapshot : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public GraphSpace GraphSpace { get; set; }
    public int NodeCount { get; set; }
    public int RelationshipCount { get; set; }
    public required string SnapshotJson { get; set; }
    public required string ChecksumSha256 { get; set; }
    public required string SafeSummary { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GraphDiff : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid FromSnapshotId { get; set; }
    public GraphSnapshot? FromSnapshot { get; set; }
    public Guid ToSnapshotId { get; set; }
    public GraphSnapshot? ToSnapshot { get; set; }
    public required string DiffJson { get; set; }
    public required string ChecksumSha256 { get; set; }
    public required string SafeSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
