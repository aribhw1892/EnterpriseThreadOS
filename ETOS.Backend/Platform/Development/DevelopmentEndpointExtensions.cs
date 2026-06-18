using ETOS.Backend.Identity;

namespace ETOS.Backend.Platform.Development;

public static class DevelopmentEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadDevelopmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/development")
            .RequireAuthorization()
            .WithTags("Development");

        group.MapPost("/clean-demo-data", async (
            IDevelopmentDemoDataCleaner cleaner,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => cleaner.CleanTenantDemoDataAsync(cancellationToken)));

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
