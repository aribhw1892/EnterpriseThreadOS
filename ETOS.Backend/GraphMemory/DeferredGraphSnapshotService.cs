using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GraphMemory;

public sealed class DeferredGraphSnapshotService(
    EnterpriseThreadDbContext dbContext,
    IGraphMemoryService graphMemoryService,
    ITenantContextResolver tenantContextResolver,
    IAuditRecorder auditRecorder) : IGraphSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GraphSnapshotContract> CaptureAsync(Guid tenantId, GraphSpace graphSpace, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("graph.snapshots.capture", cancellationToken);
        if (context.TenantId != tenantId)
        {
            throw new TenantAccessDeniedException("Graph snapshot tenant does not match active tenant.");
        }

        var graph = await graphMemoryService.ListGraphAsync(tenantId, graphSpace, null, null, null, cancellationToken);
        var identityLinks = await dbContext.IdentityCandidateLinks
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId)
            .OrderBy(link => link.Id)
            .Select(link => new SnapshotIdentityLink(
                link.Id,
                link.SourceGraphNodeId,
                link.TargetGraphNodeId,
                link.State.ToString(),
                link.TrustState.ToString(),
                link.GraphRelationshipId))
            .ToListAsync(cancellationToken);
        var dataQuality = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(issue => issue.TenantId == tenantId)
            .OrderBy(issue => issue.Id)
            .Select(issue => new SnapshotDataQualityIssue(
                issue.Id,
                issue.Status.ToString(),
                issue.Severity.ToString(),
                issue.AffectedEntityType.ToString(),
                issue.GraphNodeId,
                issue.GraphRelationshipId,
                issue.IdentityCandidateLinkId))
            .ToListAsync(cancellationToken);
        var payload = new SnapshotPayload(
            graph.Nodes.OrderBy(node => node.NodeId).Select(ToSnapshotNode).ToList(),
            graph.Relationships.OrderBy(relationship => relationship.RelationshipId).Select(ToSnapshotRelationship).ToList(),
            identityLinks,
            dataQuality);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var checksum = Sha256(json);
        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                tenantId,
                context.UserId,
                "graph.snapshots.capture",
                AuditResult.Success,
                null,
                $"Captured {graphSpace} graph snapshot with {payload.Nodes.Count} node(s) and {payload.Relationships.Count} relationship(s).",
                SourceObjectType: nameof(GraphSnapshot),
                SourceObjectId: null,
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        var snapshot = new GraphSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GraphSpace = graphSpace,
            NodeCount = payload.Nodes.Count,
            RelationshipCount = payload.Relationships.Count,
            SnapshotJson = json,
            ChecksumSha256 = checksum,
            SafeSummary = $"Captured {graphSpace} graph snapshot with {payload.Nodes.Count} node(s), {payload.Relationships.Count} relationship(s), {payload.IdentityLinks.Count} identity link(s), and {payload.DataQualityIssues.Count} data-quality issue(s).",
            CreatedByUserId = context.UserId,
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.GraphSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToContract(snapshot);
    }

    public async Task<IReadOnlyCollection<GraphSnapshotContract>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.GraphSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId)
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .Select(snapshot => ToContract(snapshot))
            .ToListAsync(cancellationToken);
    }

    public async Task<GraphSnapshotContract> GetAsync(Guid tenantId, Guid snapshotId, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.GraphSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == snapshotId, cancellationToken)
            ?? throw new RequestValidationException("Graph snapshot was not found.");
        return ToContract(snapshot);
    }

    private static SnapshotNode ToSnapshotNode(BaseNode node)
    {
        return new SnapshotNode(
            node.NodeId,
            node.GraphSpace.ToString(),
            node.ObjectType,
            node.TrustState.ToString(),
            node.Attributes.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.Value),
            node.SourceReference);
    }

    private static SnapshotRelationship ToSnapshotRelationship(BaseRelationship relationship)
    {
        return new SnapshotRelationship(
            relationship.RelationshipId,
            relationship.FromNodeId,
            relationship.ToNodeId,
            relationship.RelationshipType,
            relationship.TrustState.ToString(),
            relationship.Attributes.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.Value),
            relationship.SourceReference);
    }

    private static GraphSnapshotContract ToContract(GraphSnapshot snapshot)
    {
        return new GraphSnapshotContract(
            snapshot.Id,
            snapshot.TenantId,
            snapshot.GraphSpace,
            snapshot.CreatedAt,
            snapshot.NodeCount,
            snapshot.RelationshipCount,
            snapshot.ChecksumSha256,
            snapshot.SafeSummary);
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    internal sealed record SnapshotPayload(
        IReadOnlyCollection<SnapshotNode> Nodes,
        IReadOnlyCollection<SnapshotRelationship> Relationships,
        IReadOnlyCollection<SnapshotIdentityLink> IdentityLinks,
        IReadOnlyCollection<SnapshotDataQualityIssue> DataQualityIssues);

    internal sealed record SnapshotNode(
        Guid NodeId,
        string GraphSpace,
        string ObjectType,
        string TrustState,
        IReadOnlyDictionary<string, string?> Attributes,
        GraphSourceReference? SourceReference);

    internal sealed record SnapshotRelationship(
        Guid RelationshipId,
        Guid FromNodeId,
        Guid ToNodeId,
        string RelationshipType,
        string TrustState,
        IReadOnlyDictionary<string, string?> Attributes,
        GraphSourceReference? SourceReference);

    internal sealed record SnapshotIdentityLink(
        Guid Id,
        Guid SourceGraphNodeId,
        Guid TargetGraphNodeId,
        string State,
        string TrustState,
        Guid? GraphRelationshipId);

    internal sealed record SnapshotDataQualityIssue(
        Guid Id,
        string Status,
        string Severity,
        string AffectedEntityType,
        Guid? GraphNodeId,
        Guid? GraphRelationshipId,
        Guid? IdentityCandidateLinkId);
}
