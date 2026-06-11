using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GraphMemory;

public sealed class DeferredGraphDiffService(EnterpriseThreadDbContext dbContext) : IGraphDiffService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GraphDiffContract> CreateDiffAsync(
        Guid tenantId,
        Guid fromSnapshotId,
        Guid toSnapshotId,
        CancellationToken cancellationToken)
    {
        var fromSnapshot = await RequireSnapshotAsync(tenantId, fromSnapshotId, cancellationToken);
        var toSnapshot = await RequireSnapshotAsync(tenantId, toSnapshotId, cancellationToken);
        var fromPayload = Deserialize(fromSnapshot.SnapshotJson);
        var toPayload = Deserialize(toSnapshot.SnapshotJson);
        var payload = BuildDiff(fromPayload, toPayload);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var checksum = Sha256(json);
        var diff = new GraphDiff
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromSnapshotId = fromSnapshotId,
            ToSnapshotId = toSnapshotId,
            DiffJson = json,
            ChecksumSha256 = checksum,
            SafeSummary = $"Diff found {payload.NodeAdditions.Count} node addition(s), {payload.NodeRemovals.Count} node removal(s), {payload.RelationshipAdditions.Count} relationship addition(s), {payload.RelationshipRemovals.Count} relationship removal(s), {payload.AttributeChanges.Count} attribute change(s), {payload.IdentityLinkChanges.Count} identity-link change(s), and {payload.DataQualityChanges.Count} data-quality change(s).",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.GraphDiffs.Add(diff);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToContract(diff);
    }

    public async Task<GraphDiffContract> GetAsync(Guid tenantId, Guid diffId, CancellationToken cancellationToken)
    {
        var diff = await dbContext.GraphDiffs
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == diffId, cancellationToken)
            ?? throw new RequestValidationException("Graph diff was not found.");
        return ToContract(diff);
    }

    private async Task<GraphSnapshot> RequireSnapshotAsync(Guid tenantId, Guid snapshotId, CancellationToken cancellationToken)
    {
        return await dbContext.GraphSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.TenantId == tenantId && snapshot.Id == snapshotId, cancellationToken)
            ?? throw new RequestValidationException("Graph snapshot was not found.");
    }

    private static GraphDiffPayload BuildDiff(
        DeferredGraphSnapshotService.SnapshotPayload from,
        DeferredGraphSnapshotService.SnapshotPayload to)
    {
        var fromNodes = from.Nodes.ToDictionary(node => node.NodeId);
        var toNodes = to.Nodes.ToDictionary(node => node.NodeId);
        var fromRelationships = from.Relationships.ToDictionary(relationship => relationship.RelationshipId);
        var toRelationships = to.Relationships.ToDictionary(relationship => relationship.RelationshipId);
        var fromIdentity = from.IdentityLinks.ToDictionary(link => link.Id);
        var toIdentity = to.IdentityLinks.ToDictionary(link => link.Id);
        var fromQuality = from.DataQualityIssues.ToDictionary(issue => issue.Id);
        var toQuality = to.DataQualityIssues.ToDictionary(issue => issue.Id);

        var attributeChanges = new List<GraphAttributeChange>();
        foreach (var nodeId in fromNodes.Keys.Intersect(toNodes.Keys).Order())
        {
            var left = fromNodes[nodeId];
            var right = toNodes[nodeId];
            foreach (var key in left.Attributes.Keys.Union(right.Attributes.Keys).Order(StringComparer.Ordinal))
            {
                left.Attributes.TryGetValue(key, out var before);
                right.Attributes.TryGetValue(key, out var after);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    attributeChanges.Add(new GraphAttributeChange("node", nodeId, key, before, after));
                }
            }
        }

        var relationshipChanges = toRelationships.Values
            .Where(relationship => fromRelationships.TryGetValue(relationship.RelationshipId, out var previous)
                && (previous.FromNodeId != relationship.FromNodeId
                    || previous.ToNodeId != relationship.ToNodeId
                    || !string.Equals(previous.RelationshipType, relationship.RelationshipType, StringComparison.Ordinal)
                    || !string.Equals(previous.TrustState, relationship.TrustState, StringComparison.Ordinal)))
            .Select(relationship => relationship.RelationshipId)
            .Order()
            .ToList();
        foreach (var relationshipId in fromRelationships.Keys.Intersect(toRelationships.Keys).Order())
        {
            var left = fromRelationships[relationshipId];
            var right = toRelationships[relationshipId];
            foreach (var key in left.Attributes.Keys.Union(right.Attributes.Keys).Order(StringComparer.Ordinal))
            {
                left.Attributes.TryGetValue(key, out var before);
                right.Attributes.TryGetValue(key, out var after);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    attributeChanges.Add(new GraphAttributeChange("relationship", relationshipId, key, before, after));
                }
            }
        }

        return new GraphDiffPayload(
            toNodes.Keys.Except(fromNodes.Keys).Order().ToList(),
            fromNodes.Keys.Except(toNodes.Keys).Order().ToList(),
            toRelationships.Keys.Except(fromRelationships.Keys).Order().ToList(),
            fromRelationships.Keys.Except(toRelationships.Keys).Order().ToList(),
            relationshipChanges,
            attributeChanges,
            ChangedIds(fromIdentity, toIdentity),
            ChangedIds(fromQuality, toQuality));
    }

    private static IReadOnlyCollection<Guid> ChangedIds<T>(Dictionary<Guid, T> from, Dictionary<Guid, T> to)
    {
        return from.Keys.Union(to.Keys)
            .Where(id => !from.TryGetValue(id, out var previous)
                || !to.TryGetValue(id, out var current)
                || !EqualityComparer<T>.Default.Equals(previous, current))
            .Order()
            .ToList();
    }

    private static DeferredGraphSnapshotService.SnapshotPayload Deserialize(string json)
    {
        return JsonSerializer.Deserialize<DeferredGraphSnapshotService.SnapshotPayload>(json, JsonOptions)
            ?? new DeferredGraphSnapshotService.SnapshotPayload([], [], [], []);
    }

    private static GraphDiffContract ToContract(GraphDiff diff)
    {
        return new GraphDiffContract(
            diff.Id,
            diff.TenantId,
            diff.FromSnapshotId,
            diff.ToSnapshotId,
            diff.CreatedAt,
            diff.ChecksumSha256,
            diff.SafeSummary);
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed record GraphDiffPayload(
        IReadOnlyCollection<Guid> NodeAdditions,
        IReadOnlyCollection<Guid> NodeRemovals,
        IReadOnlyCollection<Guid> RelationshipAdditions,
        IReadOnlyCollection<Guid> RelationshipRemovals,
        IReadOnlyCollection<Guid> RelationshipChanges,
        IReadOnlyCollection<GraphAttributeChange> AttributeChanges,
        IReadOnlyCollection<Guid> IdentityLinkChanges,
        IReadOnlyCollection<Guid> DataQualityChanges);

    private sealed record GraphAttributeChange(
        string TargetType,
        Guid TargetId,
        string AttributeKey,
        string? Before,
        string? After);
}
