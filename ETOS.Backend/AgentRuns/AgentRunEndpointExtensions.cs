using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentRuns;

public static class AgentRunEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadAgentRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/agent-runs")
            .RequireAuthorization()
            .WithTags("AgentRuns");

        group.MapGet("/", async (
            IAgentRunService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapGet("/{agentRunId:guid}", async (
            Guid agentRunId,
            IAgentRunService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(agentRunId, cancellationToken)));

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
