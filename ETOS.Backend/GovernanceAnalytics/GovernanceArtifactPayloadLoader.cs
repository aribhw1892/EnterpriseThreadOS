using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernanceAnalytics;

internal sealed record LoadedArtifactPayload<T>(
    Artifact Artifact,
    ArtifactVersion Version,
    T Payload);

internal static class GovernanceArtifactPayloadLoader
{
    private static readonly HashSet<string> LegacyDecisionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DECISION",
        "DECISION-ARTIFACT",
        "DECISIONARTIFACT"
    };

    public static async Task<IReadOnlyCollection<LoadedArtifactPayload<ReviewTaskPayloadParser.ReviewTaskPayloadDocument>>> LoadReviewTasksAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        int take,
        CancellationToken cancellationToken)
        => await LoadAsync(
            dbContext,
            tenantId,
            ReviewTaskArtifactTypes.ReviewTask.ToUpperInvariant(),
            version => ReviewTaskPayloadParser.Deserialize(version.PayloadJson ?? "{}"),
            take,
            cancellationToken);

    public static async Task<IReadOnlyCollection<LoadedArtifactPayload<DecisionPayloadParser.DecisionPayloadDocument>>> LoadDecisionsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedType = DecisionArtifactTypes.Decision.ToUpperInvariant();
        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId
                && (artifact.NormalizedArtifactType == normalizedType || LegacyDecisionTypes.Contains(artifact.NormalizedArtifactType)))
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await MapLatestPayloadsAsync(
            dbContext,
            tenantId,
            artifacts,
            version => DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}"),
            cancellationToken);
    }

    public static async Task<IReadOnlyCollection<LoadedArtifactPayload<RecommendationPayloadParser.RecommendationPayloadDocument>>> LoadRecommendationsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        int take,
        CancellationToken cancellationToken)
        => await LoadAsync(
            dbContext,
            tenantId,
            RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            version => RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}"),
            take,
            cancellationToken);

    private static async Task<IReadOnlyCollection<LoadedArtifactPayload<T>>> LoadAsync<T>(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        string normalizedArtifactType,
        Func<ArtifactVersion, T> deserialize,
        int take,
        CancellationToken cancellationToken)
    {
        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedArtifactType)
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await MapLatestPayloadsAsync(dbContext, tenantId, artifacts, deserialize, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<LoadedArtifactPayload<T>>> MapLatestPayloadsAsync<T>(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        IReadOnlyCollection<Artifact> artifacts,
        Func<ArtifactVersion, T> deserialize,
        CancellationToken cancellationToken)
    {
        if (artifacts.Count == 0)
        {
            return [];
        }

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.TenantId == tenantId && artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        var results = new List<LoadedArtifactPayload<T>>();
        foreach (var artifact in artifacts)
        {
            if (!versionLookup.TryGetValue(artifact.Id, out var version))
            {
                continue;
            }

            results.Add(new LoadedArtifactPayload<T>(artifact, version, deserialize(version)));
        }

        return results;
    }
}
