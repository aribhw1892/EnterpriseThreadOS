namespace ETOS.Backend.GovernanceAnalytics;

public static class GovernanceAnalyticsPermissions
{
    public const string Read = "governance_analytics.read";
}

public static class CustomKpiArtifactTypes
{
    public const string CustomKpiDefinition = "CustomKpiDefinitionVersion";
}

public static class PlatformGovernanceKpiKeys
{
    public const string OpenReviews = "open_reviews";
    public const string PendingDecisions = "pending_decisions";
    public const string BlockedDecisions = "blocked_decisions";
    public const string Escalations = "escalations";
    public const string DecisionThroughput = "decision_throughput";
    public const string OutcomeVerificationRate = "outcome_verification_rate";
    public const string LearningSignalRate = "learning_signal_rate";
    public const string HighRiskRecommendations = "high_risk_recommendations";
    public const string TenantCustomKpi = "tenant_custom_kpi";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        OpenReviews,
        PendingDecisions,
        BlockedDecisions,
        Escalations,
        DecisionThroughput,
        OutcomeVerificationRate,
        LearningSignalRate,
        HighRiskRecommendations,
        TenantCustomKpi
    };

    public static readonly IReadOnlySet<string> TrendSupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        OpenReviews,
        BlockedDecisions,
        DecisionThroughput,
        OutcomeVerificationRate,
        LearningSignalRate
    };
}

public static class PlatformGovernanceKpiPlaceholders
{
    public static readonly IReadOnlyCollection<GovernanceKpiPlaceholderResponse> Catalog =
    [
        new(PlatformGovernanceKpiKeys.OpenReviews, "Open Reviews", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.PendingDecisions, "Pending Decisions", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.BlockedDecisions, "Blocked Decisions", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.Escalations, "Escalations", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.DecisionThroughput, "Decision Throughput", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.OutcomeVerificationRate, "Outcome Verification Rate", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.LearningSignalRate, "Learning Signal Rate", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.HighRiskRecommendations, "High-Risk Recommendations", "Milestone 4 governance analytics.", "platform_defined"),
        new(PlatformGovernanceKpiKeys.TenantCustomKpi, "Custom KPI (future)", "Tenant-defined KPI definitions deferred.", "tenant_custom_deferred")
    ];
}

public sealed record GovernanceKpiPlaceholderResponse(
    string KpiKey,
    string Title,
    string Notes,
    string Source);

public sealed record GovernanceKpiValueResponse(
    string KpiKey,
    string Title,
    string Source,
    decimal? Value,
    string? Unit,
    string? FormattedValue,
    string Status);

public sealed record GovernanceKpiTrendPointResponse(
    DateTimeOffset BucketStart,
    decimal Value);

public sealed record GovernanceKpiTrendResponse(
    string KpiKey,
    int WindowDays,
    string Bucket,
    IReadOnlyCollection<GovernanceKpiTrendPointResponse> Points);

public sealed record HighRiskRecommendationSummaryResponse(
    Guid ArtifactId,
    string Title,
    string RiskState,
    string LifecycleStatus,
    string ContextViewRoute);

public sealed record GovernanceGraphSupplementResponse(
    int MaxDecisionChainDepth,
    int UnresolvedUpstreamReviewCount);

public sealed record GovernanceDashboardResponse(
    IReadOnlyCollection<GovernanceKpiValueResponse> Kpis,
    IReadOnlyCollection<HighRiskRecommendationSummaryResponse> HighRiskRecommendations,
    GovernanceGraphSupplementResponse? GraphSupplements,
    int WindowDays,
    DateTimeOffset GeneratedAt);

public sealed class GovernanceAnalyticsOptions
{
    public const string SectionName = "GovernanceAnalytics";

    public int DefaultWindowDays { get; set; } = 30;
}
