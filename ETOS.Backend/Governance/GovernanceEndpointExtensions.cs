using ETOS.Backend.Identity;

namespace ETOS.Backend.Governance;

public static class GovernanceEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadGovernanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/governance")
            .RequireAuthorization()
            .WithTags("Governance");

        group.MapGet("/audit-records", async (
            string? result,
            string? action,
            int? limit,
            IAuditExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAuditRecordsAsync(result, action, limit, cancellationToken)));

        group.MapGet("/security-events", async (
            string? eventType,
            string? severity,
            int? limit,
            IAuditExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListSecurityEventsAsync(eventType, severity, limit, cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return Results.Ok(await action());
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
