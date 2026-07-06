using ETOS.Backend.Identity;
using ETOS.Backend.Outcomes;

namespace ETOS.Backend.Decisions;

public static class DecisionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadDecisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/decisions")
            .RequireAuthorization()
            .WithTags("Decisions");

        group.MapGet("/", async (
            string? status,
            string? conflict,
            string? outcomeKey,
            IDecisionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(status, conflict, outcomeKey, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IDecisionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/votes", async (
            Guid artifactId,
            Guid versionId,
            CastDecisionVoteRequest request,
            IDecisionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CastVoteAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/comments", async (
            Guid artifactId,
            Guid versionId,
            AddDecisionCommentRequest request,
            IDecisionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddCommentAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/finalize", async (
            Guid artifactId,
            Guid versionId,
            IDecisionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.FinalizeAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/escalation", async (
            Guid artifactId,
            Guid versionId,
            IDecisionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateEscalationAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/outcomes", async (
            Guid artifactId,
            Guid versionId,
            RecordManualOutcomeRequest request,
            IOutcomeService outcomeService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => outcomeService.RecordManualOutcomeAsync(artifactId, versionId, request, cancellationToken)));

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
