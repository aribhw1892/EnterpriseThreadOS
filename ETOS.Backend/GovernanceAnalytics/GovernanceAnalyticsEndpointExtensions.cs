using ETOS.Backend.Identity;

namespace ETOS.Backend.GovernanceAnalytics;

public static class GovernanceAnalyticsEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadGovernanceAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/governance-analytics")
            .RequireAuthorization()
            .WithTags("Governance Analytics");

        group.MapGet("/dashboard", async (
            int? windowDays,
            IGovernanceAnalyticsService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetDashboardAsync(windowDays, cancellationToken)));

        group.MapGet("/kpis", async (
            int? windowDays,
            IGovernanceAnalyticsService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListKpisAsync(windowDays, cancellationToken)));

        group.MapGet("/kpis/{kpiKey}/trends", async (
            string kpiKey,
            int? windowDays,
            IGovernanceAnalyticsService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetKpiTrendAsync(kpiKey, windowDays, cancellationToken)));

        group.MapGet("/high-risk-recommendations", async (
            int? limit,
            IGovernanceAnalyticsService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListHighRiskRecommendationsAsync(limit, cancellationToken)));

        group.MapGet("/kpi-placeholders", async (
            IGovernanceAnalyticsService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListKpiPlaceholdersAsync(cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (RequestValidationException exception)
        {
            return Results.BadRequest(new ProblemResponse(exception.Message));
        }
        catch (TenantAccessDeniedException exception)
        {
            return Results.Problem(
                title: "Forbidden",
                detail: exception.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
