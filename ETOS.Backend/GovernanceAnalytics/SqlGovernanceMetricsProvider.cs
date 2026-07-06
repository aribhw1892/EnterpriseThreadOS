using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.Outcomes;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernanceAnalytics;

public interface ISqlGovernanceMetricsProvider
{
    Task<GovernanceMetricsSnapshot> ComputeSnapshotAsync(
        Guid tenantId,
        int windowDays,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> ComputeTrendAsync(
        Guid tenantId,
        string kpiKey,
        int windowDays,
        CancellationToken cancellationToken);
}

public sealed record GovernanceMetricsSnapshot(
    int OpenReviews,
    int PendingDecisions,
    int BlockedDecisions,
    int Escalations,
    int DecisionThroughput,
    decimal OutcomeVerificationRate,
    decimal LearningSignalRate,
    int HighRiskRecommendations,
    IReadOnlyCollection<HighRiskRecommendationSummaryResponse> HighRiskRecommendationItems);

public sealed class SqlGovernanceMetricsProvider(EnterpriseThreadDbContext dbContext) : ISqlGovernanceMetricsProvider
{
    private const int ArtifactLoadLimit = 500;

    private static readonly HashSet<ReviewTaskStatus> OpenReviewStatuses =
    [
        ReviewTaskStatus.Open,
        ReviewTaskStatus.InReview,
        ReviewTaskStatus.Blocked,
        ReviewTaskStatus.NeedsReevaluation
    ];

    public async Task<GovernanceMetricsSnapshot> ComputeSnapshotAsync(
        Guid tenantId,
        int windowDays,
        CancellationToken cancellationToken)
    {
        var windowStart = DateTimeOffset.UtcNow.AddDays(-windowDays);
        var reviewTasks = await GovernanceArtifactPayloadLoader.LoadReviewTasksAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);
        var decisions = await GovernanceArtifactPayloadLoader.LoadDecisionsAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);
        var recommendations = await GovernanceArtifactPayloadLoader.LoadRecommendationsAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);

        var openReviews = reviewTasks.Count(item => OpenReviewStatuses.Contains(item.Payload.Status));
        var pendingDecisions = decisions.Count(item => item.Payload.Status == DecisionStatus.PendingVotes);
        var blockedDecisions = decisions.Count(item =>
            item.Payload.Status == DecisionStatus.BlockedConflict
            || item.Payload.ConflictState == DecisionConflictState.Blocked);
        var escalations = await CountEscalationsAsync(tenantId, reviewTasks, decisions, cancellationToken);

        var finalizedInWindow = decisions
            .Where(item => item.Payload.Status == DecisionStatus.Finalized)
            .Where(item => ResolveDecisionTimestamp(item.Payload, item.Artifact) >= windowStart)
            .ToList();
        var decisionThroughput = finalizedInWindow.Count;

        var outcomeVerificationRate = await ComputeOutcomeVerificationRateAsync(
            tenantId,
            decisions.Select(item => item.Artifact.Id).ToArray(),
            cancellationToken);

        var learningSignalRate = await ComputeLearningSignalRateAsync(
            tenantId,
            windowStart,
            finalizedInWindow.Count,
            cancellationToken);

        var highRiskItems = recommendations
            .Where(item => item.Payload.RiskState is RecommendationRiskState.High or RecommendationRiskState.Critical)
            .Where(item => item.Payload.LifecycleStatus is not (RecommendationLifecycleStatus.Accepted or RecommendationLifecycleStatus.Rejected))
            .Select(item => new HighRiskRecommendationSummaryResponse(
                item.Artifact.Id,
                item.Payload.Title?.Trim() ?? item.Artifact.Name,
                item.Payload.RiskState.ToString(),
                item.Payload.LifecycleStatus.ToString(),
                $"/recommendations/{item.Artifact.Id}"))
            .ToList();

        return new GovernanceMetricsSnapshot(
            openReviews,
            pendingDecisions,
            blockedDecisions,
            escalations,
            decisionThroughput,
            outcomeVerificationRate,
            learningSignalRate,
            highRiskItems.Count,
            highRiskItems);
    }

    public async Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> ComputeTrendAsync(
        Guid tenantId,
        string kpiKey,
        int windowDays,
        CancellationToken cancellationToken)
    {
        if (!PlatformGovernanceKpiKeys.TrendSupported.Contains(kpiKey))
        {
            throw new RequestValidationException($"Trend analytics are not supported for KPI '{kpiKey}'.");
        }

        var windowStart = DateTimeOffset.UtcNow.Date.AddDays(-windowDays + 1);
        var bucketStarts = Enumerable.Range(0, windowDays)
            .Select(offset => new DateTimeOffset(windowStart.AddDays(offset), TimeSpan.Zero))
            .ToList();

        return kpiKey switch
        {
            PlatformGovernanceKpiKeys.OpenReviews => await BuildOpenReviewTrendAsync(tenantId, bucketStarts, cancellationToken),
            PlatformGovernanceKpiKeys.BlockedDecisions => await BuildBlockedDecisionTrendAsync(tenantId, bucketStarts, cancellationToken),
            PlatformGovernanceKpiKeys.DecisionThroughput => await BuildDecisionThroughputTrendAsync(tenantId, bucketStarts, cancellationToken),
            PlatformGovernanceKpiKeys.OutcomeVerificationRate => await BuildOutcomeVerificationTrendAsync(tenantId, bucketStarts, cancellationToken),
            PlatformGovernanceKpiKeys.LearningSignalRate => await BuildLearningSignalTrendAsync(tenantId, bucketStarts, cancellationToken),
            _ => throw new RequestValidationException($"Trend analytics are not supported for KPI '{kpiKey}'.")
        };
    }

    private async Task<int> CountEscalationsAsync(
        Guid tenantId,
        IReadOnlyCollection<LoadedArtifactPayload<ReviewTaskPayloadParser.ReviewTaskPayloadDocument>> reviewTasks,
        IReadOnlyCollection<LoadedArtifactPayload<DecisionPayloadParser.DecisionPayloadDocument>> decisions,
        CancellationToken cancellationToken)
    {
        var escalationTaskCount = reviewTasks.Count(item =>
            OpenReviewStatuses.Contains(item.Payload.Status)
            && (item.Payload.Title?.Contains(":escalation", StringComparison.OrdinalIgnoreCase) == true
                || item.Payload.SourceReference?.Contains(":escalation", StringComparison.OrdinalIgnoreCase) == true
                || item.Payload.Title?.StartsWith("Escalation for", StringComparison.OrdinalIgnoreCase) == true));

        var escalatedDecisions = decisions.Count(item => item.Payload.Status == DecisionStatus.Escalated);

        var blockedDecisionIds = decisions
            .Where(item => item.Payload.Status == DecisionStatus.BlockedConflict)
            .Select(item => item.Artifact.Id)
            .ToHashSet();
        var linkedEscalationTasks = 0;
        if (blockedDecisionIds.Count > 0)
        {
            linkedEscalationTasks = await dbContext.ArtifactRelationships
                .AsNoTracking()
                .CountAsync(
                    relationship => relationship.TenantId == tenantId
                        && blockedDecisionIds.Contains(relationship.SourceArtifactId)
                        && relationship.RelationshipType == ArtifactRelationshipType.DerivedFrom,
                    cancellationToken);
        }

        return escalationTaskCount + escalatedDecisions + linkedEscalationTasks;
    }

    private async Task<decimal> ComputeOutcomeVerificationRateAsync(
        Guid tenantId,
        Guid[] decisionArtifactIds,
        CancellationToken cancellationToken)
    {
        if (decisionArtifactIds.Length == 0)
        {
            return 0m;
        }

        var runs = await dbContext.OutcomeCheckRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && decisionArtifactIds.Contains(run.DecisionArtifactId))
            .ToListAsync(cancellationToken);
        if (runs.Count == 0)
        {
            return 0m;
        }

        var decisionsWithChecks = runs.Select(run => run.DecisionArtifactId).Distinct().Count();
        var successfulChecks = runs.Count(run => run.OutcomeStatus == OutcomeCheckStatus.Successful);
        return Math.Round((decimal)successfulChecks / decisionsWithChecks, 4);
    }

    private async Task<decimal> ComputeLearningSignalRateAsync(
        Guid tenantId,
        DateTimeOffset windowStart,
        int finalizedDecisionCount,
        CancellationToken cancellationToken)
    {
        if (finalizedDecisionCount == 0)
        {
            return 0m;
        }

        var normalizedType = LearningArtifactTypes.LearningSignal.ToUpperInvariant();
        var signalCount = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.TenantId == tenantId && version.CreatedAt >= windowStart)
            .Join(
                dbContext.Artifacts.Where(artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedType),
                version => version.ArtifactId,
                artifact => artifact.Id,
                (version, _) => version)
            .CountAsync(cancellationToken);

        return Math.Round((decimal)signalCount / finalizedDecisionCount, 4);
    }

    private async Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> BuildOpenReviewTrendAsync(
        Guid tenantId,
        IReadOnlyCollection<DateTimeOffset> bucketStarts,
        CancellationToken cancellationToken)
    {
        var reviewTasks = await GovernanceArtifactPayloadLoader.LoadReviewTasksAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);

        return bucketStarts
            .Select(bucketStart =>
            {
                var bucketEnd = bucketStart.AddDays(1);
                var value = reviewTasks.Count(item =>
                    OpenReviewStatuses.Contains(item.Payload.Status)
                    && item.Artifact.UpdatedAt >= bucketStart
                    && item.Artifact.UpdatedAt < bucketEnd);
                return new GovernanceKpiTrendPointResponse(bucketStart, value);
            })
            .ToList();
    }

    private async Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> BuildBlockedDecisionTrendAsync(
        Guid tenantId,
        IReadOnlyCollection<DateTimeOffset> bucketStarts,
        CancellationToken cancellationToken)
    {
        var decisions = await GovernanceArtifactPayloadLoader.LoadDecisionsAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);

        return bucketStarts
            .Select(bucketStart =>
            {
                var bucketEnd = bucketStart.AddDays(1);
                var value = decisions.Count(item =>
                    (item.Payload.Status == DecisionStatus.BlockedConflict || item.Payload.ConflictState == DecisionConflictState.Blocked)
                    && item.Artifact.UpdatedAt >= bucketStart
                    && item.Artifact.UpdatedAt < bucketEnd);
                return new GovernanceKpiTrendPointResponse(bucketStart, value);
            })
            .ToList();
    }

    private async Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> BuildDecisionThroughputTrendAsync(
        Guid tenantId,
        IReadOnlyCollection<DateTimeOffset> bucketStarts,
        CancellationToken cancellationToken)
    {
        var decisions = await GovernanceArtifactPayloadLoader.LoadDecisionsAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);

        return bucketStarts
            .Select(bucketStart =>
            {
                var bucketEnd = bucketStart.AddDays(1);
                var value = decisions.Count(item =>
                {
                    if (item.Payload.Status != DecisionStatus.Finalized)
                    {
                        return false;
                    }

                    var timestamp = ResolveDecisionTimestamp(item.Payload, item.Artifact);
                    return timestamp >= bucketStart && timestamp < bucketEnd;
                });
                return new GovernanceKpiTrendPointResponse(bucketStart, value);
            })
            .ToList();
    }

    private async Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> BuildOutcomeVerificationTrendAsync(
        Guid tenantId,
        IReadOnlyCollection<DateTimeOffset> bucketStarts,
        CancellationToken cancellationToken)
    {
        var runs = await dbContext.OutcomeCheckRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId)
            .OrderBy(run => run.MeasuredAt)
            .ToListAsync(cancellationToken);

        return bucketStarts
            .Select(bucketStart =>
            {
                var bucketEnd = bucketStart.AddDays(1);
                var bucketRuns = runs
                    .Where(run => run.MeasuredAt >= bucketStart && run.MeasuredAt < bucketEnd)
                    .ToList();
                if (bucketRuns.Count == 0)
                {
                    return new GovernanceKpiTrendPointResponse(bucketStart, 0m);
                }

                var decisionsWithChecks = bucketRuns.Select(run => run.DecisionArtifactId).Distinct().Count();
                var successfulChecks = bucketRuns.Count(run => run.OutcomeStatus == OutcomeCheckStatus.Successful);
                var rate = decisionsWithChecks == 0 ? 0m : Math.Round((decimal)successfulChecks / decisionsWithChecks, 4);
                return new GovernanceKpiTrendPointResponse(bucketStart, rate);
            })
            .ToList();
    }

    private async Task<IReadOnlyCollection<GovernanceKpiTrendPointResponse>> BuildLearningSignalTrendAsync(
        Guid tenantId,
        IReadOnlyCollection<DateTimeOffset> bucketStarts,
        CancellationToken cancellationToken)
    {
        var normalizedSignalType = LearningArtifactTypes.LearningSignal.ToUpperInvariant();

        var finalizedDecisions = await GovernanceArtifactPayloadLoader.LoadDecisionsAsync(
            dbContext,
            tenantId,
            ArtifactLoadLimit,
            cancellationToken);
        var signalVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.TenantId == tenantId)
            .Join(
                dbContext.Artifacts.Where(artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedSignalType),
                version => version.ArtifactId,
                artifact => artifact.Id,
                (version, _) => version)
            .OrderBy(version => version.CreatedAt)
            .ToListAsync(cancellationToken);

        return bucketStarts
            .Select(bucketStart =>
            {
                var bucketEnd = bucketStart.AddDays(1);
                var finalizedCount = finalizedDecisions.Count(item =>
                {
                    if (item.Payload.Status != DecisionStatus.Finalized)
                    {
                        return false;
                    }

                    var timestamp = ResolveDecisionTimestamp(item.Payload, item.Artifact);
                    return timestamp >= bucketStart && timestamp < bucketEnd;
                });
                if (finalizedCount == 0)
                {
                    return new GovernanceKpiTrendPointResponse(bucketStart, 0m);
                }

                var signalCount = signalVersions.Count(version => version.CreatedAt >= bucketStart && version.CreatedAt < bucketEnd);
                return new GovernanceKpiTrendPointResponse(bucketStart, Math.Round((decimal)signalCount / finalizedCount, 4));
            })
            .ToList();
    }

    private static DateTimeOffset ResolveDecisionTimestamp(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        Artifact artifact)
        => payload.FinalizedAt ?? artifact.UpdatedAt;
}
