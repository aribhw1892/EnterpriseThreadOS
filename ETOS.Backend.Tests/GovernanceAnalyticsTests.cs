using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Explorers;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernanceAnalytics;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.Outcomes;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class GovernanceAnalyticsTests
{
    [Fact]
    public async Task Dashboard_counts_open_reviews_and_pending_decisions()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        SeedOpenReviewTask(dbContext, context);
        SeedDecision(dbContext, context, DecisionStatus.PendingVotes, DecisionConflictState.None);

        var service = CreateAnalyticsService(dbContext, context);
        var dashboard = await service.GetDashboardAsync(30, CancellationToken.None);

        Assert.Equal(1, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.OpenReviews).Value);
        Assert.Equal(1, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.PendingDecisions).Value);
    }

    [Fact]
    public async Task Blocked_decisions_and_escalations()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        SeedDecision(dbContext, context, DecisionStatus.BlockedConflict, DecisionConflictState.Blocked);
        SeedOpenReviewTask(dbContext, context, title: "Escalation for blocked review", sourceReference: "manual:escalation");

        var service = CreateAnalyticsService(dbContext, context);
        var dashboard = await service.GetDashboardAsync(30, CancellationToken.None);

        Assert.Equal(1, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.BlockedDecisions).Value);
        Assert.True(FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.Escalations).Value >= 1);
    }

    [Fact]
    public async Task Outcome_verification_rate_uses_successful_checks()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        var decisionId = SeedDecision(dbContext, context, DecisionStatus.Finalized, DecisionConflictState.None);
        dbContext.OutcomeCheckRuns.AddRange(
            new OutcomeCheckRun
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                DecisionArtifactId = decisionId,
                CheckType = "manual",
                ExpectedOutcome = "accept",
                ActualOutcome = "accept",
                OutcomeStatus = OutcomeCheckStatus.Successful,
                EvidenceSummary = "Verified",
                RecordedByUserId = context.UserId
            },
            new OutcomeCheckRun
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                DecisionArtifactId = decisionId,
                CheckType = "manual",
                ExpectedOutcome = "accept",
                ActualOutcome = "partial",
                OutcomeStatus = OutcomeCheckStatus.Partial,
                EvidenceSummary = "Partial",
                RecordedByUserId = context.UserId
            });
        await dbContext.SaveChangesAsync();

        var service = CreateAnalyticsService(dbContext, context);
        var dashboard = await service.GetDashboardAsync(30, CancellationToken.None);

        Assert.Equal(1m, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.OutcomeVerificationRate).Value);
    }

    [Fact]
    public async Task Learning_signal_rate_counts_signals_against_finalized_decisions()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        SeedDecision(dbContext, context, DecisionStatus.Finalized, DecisionConflictState.None, finalizedAt: DateTimeOffset.UtcNow);
        SeedLearningSignalArtifact(dbContext, context);

        var service = CreateAnalyticsService(dbContext, context);
        var dashboard = await service.GetDashboardAsync(30, CancellationToken.None);

        Assert.Equal(1m, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.LearningSignalRate).Value);
    }

    [Fact]
    public async Task High_risk_recommendations_excludes_terminal_lifecycle()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        SeedRecommendation(dbContext, context, RecommendationRiskState.High, RecommendationLifecycleStatus.Reviewed);
        SeedRecommendation(dbContext, context, RecommendationRiskState.Critical, RecommendationLifecycleStatus.Accepted);

        var service = CreateAnalyticsService(dbContext, context);
        var dashboard = await service.GetDashboardAsync(30, CancellationToken.None);

        Assert.Equal(1, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.HighRiskRecommendations).Value);
        Assert.Single(dashboard.HighRiskRecommendations);
    }

    [Fact]
    public async Task Trend_aggregation_buckets_by_day()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        SeedDecision(
            dbContext,
            context,
            DecisionStatus.Finalized,
            DecisionConflictState.None,
            finalizedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var service = CreateAnalyticsService(dbContext, context);
        var trend = await service.GetKpiTrendAsync(PlatformGovernanceKpiKeys.DecisionThroughput, 7, CancellationToken.None);

        Assert.Equal(7, trend.Points.Count);
        Assert.Contains(trend.Points, point => point.Value >= 1);
    }

    [Fact]
    public async Task Tenant_isolation_denies_other_tenant_data()
    {
        await using var dbContext = CreateDbContext();
        var tenantA = SeedTenantWithMembership(dbContext);
        var tenantB = SeedTenantWithMembership(dbContext);
        SeedOpenReviewTask(dbContext, tenantA);
        SeedOpenReviewTask(dbContext, tenantB);
        SeedOpenReviewTask(dbContext, tenantB);

        var service = CreateAnalyticsService(dbContext, tenantA);
        var dashboard = await service.GetDashboardAsync(30, CancellationToken.None);

        Assert.Equal(1, FindKpi(dashboard.Kpis, PlatformGovernanceKpiKeys.OpenReviews).Value);
    }

    [Fact]
    public async Task Custom_kpi_returns_deferred()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        var service = CreateAnalyticsService(dbContext, context);

        var kpi = await service.GetKpiValueAsync(PlatformGovernanceKpiKeys.TenantCustomKpi, 30, CancellationToken.None);

        Assert.NotNull(kpi);
        Assert.Equal("deferred", kpi!.Status);
        Assert.Null(kpi.Value);
    }

    private static GovernanceKpiValueResponse FindKpi(
        IReadOnlyCollection<GovernanceKpiValueResponse> kpis,
        string kpiKey)
        => kpis.Single(item => item.KpiKey.Equals(kpiKey, StringComparison.OrdinalIgnoreCase));

    private static GovernanceAnalyticsService CreateAnalyticsService(
        EnterpriseThreadDbContext dbContext,
        TestContext context)
        => new(
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new SqlGovernanceMetricsProvider(dbContext),
            new GraphGovernanceMetricsProvider(dbContext),
            Options.Create(new GovernanceAnalyticsOptions()));

    private static Guid SeedOpenReviewTask(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        string title = "Open review",
        string sourceReference = "manual-open")
    {
        var artifactId = Guid.NewGuid();
        var payload = ReviewTaskPayloadParser.CreateDefault(
            title,
            ReviewTaskSourceType.Manual,
            sourceReference,
            "business-action-review",
            context.UserId,
            null,
            null,
            ReviewTaskPriority.Normal,
            RecommendationRiskState.Medium,
            TrustState.Unverified,
            RecommendationConflictState.None,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            ReviewTaskStatus.Open);
        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskArtifactTypes.ReviewTask,
            NormalizedArtifactType = ReviewTaskArtifactTypes.ReviewTask.ToUpperInvariant(),
            Name = title,
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = ReviewTaskPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return artifactId;
    }

    private static Guid SeedDecision(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        DecisionStatus status,
        DecisionConflictState conflictState,
        DateTimeOffset? finalizedAt = null)
    {
        var artifactId = Guid.NewGuid();
        var payload = DecisionPayloadParser.CreateFromReviewTask(
            new ReviewTaskPayloadParser.ReviewTaskPayloadDocument
            {
                Title = "Decision",
                SourceType = ReviewTaskSourceType.Manual,
                SourceReference = "manual",
                ReviewTaskType = "business-action-review"
            },
            "accept",
            "summary",
            "reason",
            DecisionPayloadParser.DefaultApprovalRule(),
            status,
            conflictState);
        payload.FinalizedAt = finalizedAt;
        payload.EvidenceReferences =
        [
            new DecisionPayloadParser.DecisionEvidenceReferenceDocument
            {
                LinkId = Guid.NewGuid(),
                EvidenceType = "GraphNode",
                SourceId = context.GraphNodeId,
                SafeSummary = "Evidence"
            }
        ];

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = DecisionArtifactTypes.Decision,
            NormalizedArtifactType = DecisionArtifactTypes.Decision.ToUpperInvariant(),
            Name = "Decision",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = finalizedAt ?? DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = DecisionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return artifactId;
    }

    private static void SeedRecommendation(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        RecommendationRiskState riskState,
        RecommendationLifecycleStatus lifecycleStatus)
    {
        var artifactId = Guid.NewGuid();
        var payload = RecommendationPayloadParser.CreateDefault(
            $"Recommendation {riskState}",
            "Summary",
            RecommendationType.DataQuality,
            RecommendationCreationSource.Manual,
            riskState,
            RecommendationCapabilityState.ReviewRequired,
            [],
            [],
            null,
            null,
            false,
            null,
            null);
        payload.LifecycleStatus = lifecycleStatus;

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = RecommendationArtifactTypes.Recommendation,
            NormalizedArtifactType = RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            Name = payload.Title!,
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = RecommendationPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static void SeedLearningSignalArtifact(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        var artifactId = Guid.NewGuid();
        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = LearningArtifactTypes.LearningSignal,
            NormalizedArtifactType = LearningArtifactTypes.LearningSignal.ToUpperInvariant(),
            Name = "Learning signal",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = """{"patternKey":"manual:accept:business-action-review","occurrenceCount":3,"summary":"Pattern detected","status":"active"}""",
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantWithMembership(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        dbContext.Tenants.Add(new Tenant
        {
            Id = context.TenantId,
            Identifier = Guid.NewGuid().ToString("N")[..8],
            NormalizedIdentifier = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name = "Demo Tenant",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Users.Add(new EtosUser
        {
            Id = context.UserId,
            UserName = $"{context.UserId:N}@example.test",
            NormalizedUserName = $"{context.UserId:N}@EXAMPLE.TEST".ToUpperInvariant(),
            Email = $"{context.UserId:N}@example.test",
            NormalizedEmail = $"{context.UserId:N}@EXAMPLE.TEST".ToUpperInvariant(),
            DisplayName = "Admin User",
            CreatedAt = DateTimeOffset.UtcNow
        });
        var roleId = Guid.NewGuid();
        dbContext.TenantRoles.Add(new TenantRole
        {
            Id = roleId,
            TenantId = context.TenantId,
            Name = "Admin",
            NormalizedName = "ADMIN",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = context.UserId,
            TenantRoleId = roleId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private sealed record TestContext(Guid TenantId, Guid UserId, Guid GraphNodeId);

    private sealed class StaticTenantContextResolver(TestContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
            => Task.FromResult(new ActiveTenantContext(context.TenantId, "demo", "Demo Tenant", context.UserId));
    }

    private sealed class AllowAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

public sealed class DecisionExplorerFilterTests
{
    [Fact]
    public async Task DecisionExplorer_filters_by_conflict_outcome_and_evidence()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        SeedLegacyDecision(
            dbContext,
            context,
            DecisionStatus.BlockedConflict,
            DecisionConflictState.Blocked,
            "accept",
            evidenceCount: 2);
        SeedLegacyDecision(
            dbContext,
            context,
            DecisionStatus.PendingVotes,
            DecisionConflictState.None,
            "",
            evidenceCount: 0);

        var service = new DecisionExplorerFoundationService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder());

        var blocked = await service.ListDecisionsAsync(
            null,
            null,
            null,
            "Blocked",
            null,
            null,
            null,
            CancellationToken.None);
        var withEvidence = await service.ListDecisionsAsync(
            null,
            null,
            null,
            null,
            null,
            null,
            2,
            CancellationToken.None);
        var withOutcome = await service.ListDecisionsAsync(
            null,
            null,
            null,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        Assert.Single(blocked);
        Assert.Single(withEvidence);
        Assert.Single(withOutcome);
    }

    private static void SeedLegacyDecision(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        DecisionStatus status,
        DecisionConflictState conflictState,
        string outcomeKey,
        int evidenceCount)
    {
        var artifactId = Guid.NewGuid();
        var payload = DecisionPayloadParser.CreateFromReviewTask(
            new ReviewTaskPayloadParser.ReviewTaskPayloadDocument
            {
                Title = $"Decision {status}",
                SourceType = ReviewTaskSourceType.Manual,
                SourceReference = "manual",
                ReviewTaskType = "business-action-review"
            },
            outcomeKey,
            "summary",
            "reason",
            DecisionPayloadParser.DefaultApprovalRule(),
            status,
            conflictState);
        payload.EvidenceReferences = Enumerable.Range(0, evidenceCount)
            .Select(_ => new DecisionPayloadParser.DecisionEvidenceReferenceDocument
            {
                LinkId = Guid.NewGuid(),
                EvidenceType = "GraphNode",
                SourceId = Guid.NewGuid(),
                SafeSummary = "Evidence"
            })
            .ToList();

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = DecisionArtifactTypes.Decision,
            NormalizedArtifactType = DecisionArtifactTypes.Decision.ToUpperInvariant(),
            Name = payload.Title!,
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = DecisionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantWithMembership(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid());
        dbContext.Tenants.Add(new Tenant
        {
            Id = context.TenantId,
            Identifier = "demo",
            NormalizedIdentifier = "DEMO",
            Name = "Demo Tenant",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Users.Add(new EtosUser
        {
            Id = context.UserId,
            UserName = "admin@example.test",
            NormalizedUserName = "ADMIN@EXAMPLE.TEST",
            Email = "admin@example.test",
            NormalizedEmail = "ADMIN@EXAMPLE.TEST",
            DisplayName = "Admin User",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private sealed record TestContext(Guid TenantId, Guid UserId);

    private sealed class StaticTenantContextResolver(TestContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
            => Task.FromResult(new ActiveTenantContext(context.TenantId, "demo", "Demo Tenant", context.UserId));
    }

    private sealed class AllowAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
