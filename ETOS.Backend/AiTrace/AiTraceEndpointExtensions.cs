using ETOS.Backend.Identity;

namespace ETOS.Backend.AiTrace;

public static class AiTraceEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadAiTraceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/ai-traces")
            .RequireAuthorization()
            .WithTags("AI Trace");

        group.MapGet("/", async (
            IAiTraceService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListTracesAsync(cancellationToken)));

        group.MapGet("/{traceId:guid}", async (
            Guid traceId,
            IAiTraceService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetTraceAsync(traceId, cancellationToken)));

        group.MapGet("/by-retrieval-run/{runId:guid}", async (
            Guid runId,
            IAiTraceService service,
            CancellationToken cancellationToken) =>
            await ExecuteOptionalAsync(() => service.GetTraceByRetrievalRunAsync(runId, cancellationToken)));

        group.MapPost("/{traceId:guid}/export", async (
            Guid traceId,
            IAiTraceService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var export = await service.ExportTraceAsync(traceId, cancellationToken);
                return Results.File(export.Content, export.ContentType, export.FileName);
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
        });

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

    private static async Task<IResult> ExecuteOptionalAsync<TResponse>(Func<Task<TResponse?>> action)
        where TResponse : class
    {
        try
        {
            var response = await action();
            return response is null ? Results.NotFound() : Results.Ok(response);
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
