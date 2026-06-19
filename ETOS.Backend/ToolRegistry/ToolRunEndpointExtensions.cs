using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class ToolRunEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadToolRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/tool-runs")
            .RequireAuthorization()
            .WithTags("ToolRuns");

        group.MapGet("/", async (
            IToolRunService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapGet("/{toolRunId:guid}", async (
            Guid toolRunId,
            IToolRunService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(toolRunId, cancellationToken)));

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
