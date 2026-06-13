using ETOS.Backend.Identity;

namespace ETOS.Backend.GovernedQuery;

public static class GovernedQueryEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadGovernedQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/governed-query")
            .RequireAuthorization()
            .WithTags("Governed Query");

        group.MapPost("/run", async (
            RunGovernedQueryRequest request,
            IGovernedQueryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RunAsync(request, cancellationToken)));

        group.MapGet("/runs", async (
            IGovernedQueryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListRunsAsync(cancellationToken)));

        group.MapGet("/runs/{runId:guid}", async (
            Guid runId,
            IGovernedQueryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetRunAsync(runId, cancellationToken)));

        group.MapGet("/context-packages/{packageId:guid}", async (
            Guid packageId,
            IGovernedQueryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetContextPackageAsync(packageId, cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            var response = await action();
            return Results.Ok(response);
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
