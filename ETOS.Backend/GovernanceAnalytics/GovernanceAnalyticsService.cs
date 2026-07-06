using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.GovernanceAnalytics;

public interface IGovernanceAnalyticsService
{
    Task<GovernanceDashboardResponse> GetDashboardAsync(int? windowDays, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GovernanceKpiValueResponse>> ListKpisAsync(int? windowDays, CancellationToken cancellationToken);

    Task<GovernanceKpiValueResponse?> GetKpiValueAsync(string kpiKey, int? windowDays, CancellationToken cancellationToken);

    Task<GovernanceKpiTrendResponse> GetKpiTrendAsync(string kpiKey, int? windowDays, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<HighRiskRecommendationSummaryResponse>> ListHighRiskRecommendationsAsync(
        int? limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GovernanceKpiPlaceholderResponse>> ListKpiPlaceholdersAsync(CancellationToken cancellationToken);
}

public sealed class GovernanceAnalyticsService(
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    ISqlGovernanceMetricsProvider sqlMetricsProvider,
    IGraphGovernanceMetricsProvider graphMetricsProvider,
    IOptions<GovernanceAnalyticsOptions> options) : IGovernanceAnalyticsService
{
    public async Task<GovernanceDashboardResponse> GetDashboardAsync(int? windowDays, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governance_analytics.dashboard", cancellationToken);
        var resolvedWindowDays = ResolveWindowDays(windowDays);
        var snapshot = await sqlMetricsProvider.ComputeSnapshotAsync(context.TenantId, resolvedWindowDays, cancellationToken);
        var graphSupplements = await graphMetricsProvider.ComputeSupplementsAsync(context.TenantId, cancellationToken);

        return new GovernanceDashboardResponse(
            BuildKpiValues(snapshot),
            snapshot.HighRiskRecommendationItems,
            graphSupplements,
            resolvedWindowDays,
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyCollection<GovernanceKpiValueResponse>> ListKpisAsync(int? windowDays, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governance_analytics.kpis.list", cancellationToken);
        var snapshot = await sqlMetricsProvider.ComputeSnapshotAsync(context.TenantId, ResolveWindowDays(windowDays), cancellationToken);
        return BuildKpiValues(snapshot);
    }

    public async Task<GovernanceKpiValueResponse?> GetKpiValueAsync(string kpiKey, int? windowDays, CancellationToken cancellationToken)
    {
        var kpis = await ListKpisAsync(windowDays, cancellationToken);
        return kpis.SingleOrDefault(item => item.KpiKey.Equals(kpiKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<GovernanceKpiTrendResponse> GetKpiTrendAsync(string kpiKey, int? windowDays, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governance_analytics.kpis.trends", cancellationToken);
        var normalizedKey = kpiKey.Trim();
        if (!PlatformGovernanceKpiKeys.All.Contains(normalizedKey))
        {
            throw new RequestValidationException($"Unknown KPI key '{kpiKey}'.");
        }

        if (normalizedKey.Equals(PlatformGovernanceKpiKeys.TenantCustomKpi, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Custom KPI trends are deferred.");
        }

        var resolvedWindowDays = ResolveWindowDays(windowDays);
        var points = await sqlMetricsProvider.ComputeTrendAsync(
            context.TenantId,
            normalizedKey,
            resolvedWindowDays,
            cancellationToken);

        return new GovernanceKpiTrendResponse(normalizedKey, resolvedWindowDays, "day", points);
    }

    public async Task<IReadOnlyCollection<HighRiskRecommendationSummaryResponse>> ListHighRiskRecommendationsAsync(
        int? limit,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governance_analytics.high_risk_recommendations", cancellationToken);
        var snapshot = await sqlMetricsProvider.ComputeSnapshotAsync(context.TenantId, ResolveWindowDays(null), cancellationToken);
        var resolvedLimit = Math.Clamp(limit ?? 50, 1, 200);
        return snapshot.HighRiskRecommendationItems.Take(resolvedLimit).ToList();
    }

    public Task<IReadOnlyCollection<GovernanceKpiPlaceholderResponse>> ListKpiPlaceholdersAsync(CancellationToken cancellationToken)
        => Task.FromResult(PlatformGovernanceKpiPlaceholders.Catalog);

    private int ResolveWindowDays(int? windowDays)
        => Math.Clamp(windowDays ?? options.Value.DefaultWindowDays, 1, 365);

    private static IReadOnlyCollection<GovernanceKpiValueResponse> BuildKpiValues(GovernanceMetricsSnapshot snapshot)
    {
        var catalog = PlatformGovernanceKpiPlaceholders.Catalog.ToDictionary(item => item.KpiKey, StringComparer.OrdinalIgnoreCase);
        return
        [
            BuildCountKpi(catalog, PlatformGovernanceKpiKeys.OpenReviews, snapshot.OpenReviews),
            BuildCountKpi(catalog, PlatformGovernanceKpiKeys.PendingDecisions, snapshot.PendingDecisions),
            BuildCountKpi(catalog, PlatformGovernanceKpiKeys.BlockedDecisions, snapshot.BlockedDecisions),
            BuildCountKpi(catalog, PlatformGovernanceKpiKeys.Escalations, snapshot.Escalations),
            BuildCountKpi(catalog, PlatformGovernanceKpiKeys.DecisionThroughput, snapshot.DecisionThroughput),
            BuildRateKpi(catalog, PlatformGovernanceKpiKeys.OutcomeVerificationRate, snapshot.OutcomeVerificationRate),
            BuildRateKpi(catalog, PlatformGovernanceKpiKeys.LearningSignalRate, snapshot.LearningSignalRate),
            BuildCountKpi(catalog, PlatformGovernanceKpiKeys.HighRiskRecommendations, snapshot.HighRiskRecommendations),
            BuildDeferredKpi(catalog, PlatformGovernanceKpiKeys.TenantCustomKpi)
        ];
    }

    private static GovernanceKpiValueResponse BuildCountKpi(
        IReadOnlyDictionary<string, GovernanceKpiPlaceholderResponse> catalog,
        string kpiKey,
        int value)
    {
        var definition = catalog[kpiKey];
        return new GovernanceKpiValueResponse(
            definition.KpiKey,
            definition.Title,
            definition.Source,
            value,
            "count",
            value.ToString(),
            "ready");
    }

    private static GovernanceKpiValueResponse BuildRateKpi(
        IReadOnlyDictionary<string, GovernanceKpiPlaceholderResponse> catalog,
        string kpiKey,
        decimal value)
    {
        var definition = catalog[kpiKey];
        return new GovernanceKpiValueResponse(
            definition.KpiKey,
            definition.Title,
            definition.Source,
            value,
            "rate",
            value.ToString("P1"),
            "ready");
    }

    private static GovernanceKpiValueResponse BuildDeferredKpi(
        IReadOnlyDictionary<string, GovernanceKpiPlaceholderResponse> catalog,
        string kpiKey)
    {
        var definition = catalog[kpiKey];
        return new GovernanceKpiValueResponse(
            definition.KpiKey,
            definition.Title,
            definition.Source,
            null,
            null,
            null,
            "deferred");
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, GovernanceAnalyticsPermissions.Read, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {GovernanceAnalyticsPermissions.Read} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks governance analytics read permission.");
        }

        return context;
    }
}
