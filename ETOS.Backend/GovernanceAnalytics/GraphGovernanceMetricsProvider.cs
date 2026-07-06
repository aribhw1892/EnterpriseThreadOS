using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernanceAnalytics;

public interface IGraphGovernanceMetricsProvider
{
    Task<GovernanceGraphSupplementResponse> ComputeSupplementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}

public sealed class GraphGovernanceMetricsProvider(EnterpriseThreadDbContext dbContext) : IGraphGovernanceMetricsProvider
{
    public async Task<GovernanceGraphSupplementResponse> ComputeSupplementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var relationships = await dbContext.ArtifactRelationships
            .AsNoTracking()
            .Where(relationship => relationship.TenantId == tenantId)
            .Select(relationship => new RelationshipEdge(
                relationship.SourceArtifactId,
                relationship.TargetArtifactId,
                relationship.RelationshipType))
            .ToListAsync(cancellationToken);

        if (relationships.Count == 0)
        {
            return new GovernanceGraphSupplementResponse(0, 0);
        }

        var normalizedDecisionType = DecisionArtifactTypes.Decision.ToUpperInvariant();
        var decisionIds = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedDecisionType)
            .Select(artifact => artifact.Id)
            .ToListAsync(cancellationToken);
        var decisionIdSet = decisionIds.ToHashSet();

        var maxDepth = 0;
        foreach (var decisionId in decisionIds)
        {
            maxDepth = Math.Max(maxDepth, MeasureUpstreamDepth(decisionId, relationships, decisionIdSet));
        }

        var unresolvedUpstream = relationships.Count(edge =>
            decisionIdSet.Contains(edge.TargetArtifactId)
            && edge.RelationshipType == ArtifactRelationshipType.DerivedFrom
            && !decisionIdSet.Contains(edge.SourceArtifactId));

        return new GovernanceGraphSupplementResponse(maxDepth, unresolvedUpstream);
    }

    private static int MeasureUpstreamDepth(
        Guid startDecisionId,
        IReadOnlyCollection<RelationshipEdge> relationships,
        IReadOnlySet<Guid> decisionIds)
    {
        var upstreamLookup = relationships
            .Where(edge => edge.RelationshipType == ArtifactRelationshipType.DerivedFrom)
            .GroupBy(edge => edge.TargetArtifactId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceArtifactId).ToList());

        var visited = new HashSet<Guid>();
        var queue = new Queue<(Guid NodeId, int Depth)>();
        queue.Enqueue((startDecisionId, 0));
        var maxDepth = 0;

        while (queue.Count > 0)
        {
            var (nodeId, depth) = queue.Dequeue();
            if (!visited.Add(nodeId))
            {
                continue;
            }

            maxDepth = Math.Max(maxDepth, depth);
            if (!upstreamLookup.TryGetValue(nodeId, out var parents))
            {
                continue;
            }

            foreach (var parentId in parents)
            {
                queue.Enqueue((parentId, depth + 1));
            }
        }

        return maxDepth;
    }

    private sealed record RelationshipEdge(
        Guid SourceArtifactId,
        Guid TargetArtifactId,
        ArtifactRelationshipType RelationshipType);
}
