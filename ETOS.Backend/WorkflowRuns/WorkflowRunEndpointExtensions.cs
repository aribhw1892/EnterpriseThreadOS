using ETOS.Backend.Identity;

namespace ETOS.Backend.WorkflowRuns;

public static class WorkflowRunEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadWorkflowRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/workflow-runs")
            .RequireAuthorization()
            .WithTags("WorkflowRuns");

        group.MapGet("/", async (
            IWorkflowRunService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapGet("/{workflowRunId:guid}", async (
            Guid workflowRunId,
            IWorkflowRunService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(workflowRunId, cancellationToken)));

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
