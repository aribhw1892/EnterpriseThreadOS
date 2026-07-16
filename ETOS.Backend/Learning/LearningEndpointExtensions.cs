using ETOS.Backend.Identity;

namespace ETOS.Backend.Learning;

public static class LearningEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/learning-signals")
            .RequireAuthorization()
            .WithTags("Learning");

        group.MapGet("/", async (
            string? status,
            string? patternKey,
            ILearningSignalService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(status, patternKey, cancellationToken)));

        group.MapGet("/{artifactId:guid}", async (
            Guid artifactId,
            ILearningSignalService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, cancellationToken)));

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
