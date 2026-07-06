using ETOS.Backend.Identity;

namespace ETOS.Backend.WorkflowRuntime;

public static class WorkflowExecutionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadWorkflowExecutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/workflows")
            .RequireAuthorization()
            .WithTags("Workflows");

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/preview", async (
            Guid artifactId,
            Guid versionId,
            WorkflowExecutionRequest request,
            IWorkflowExecutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PreviewAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/test-run", async (
            Guid artifactId,
            Guid versionId,
            WorkflowExecutionRequest request,
            IWorkflowExecutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.TestRunAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/execute", async (
            Guid artifactId,
            Guid versionId,
            WorkflowExecutionRequest request,
            IWorkflowExecutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ExecuteAsync(artifactId, versionId, request, cancellationToken)));

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
