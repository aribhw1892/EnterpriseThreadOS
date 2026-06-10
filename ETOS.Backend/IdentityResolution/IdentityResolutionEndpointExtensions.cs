using ETOS.Backend.Identity;

namespace ETOS.Backend.IdentityResolution;

public static class IdentityResolutionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadIdentityResolutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/identity-resolution")
            .RequireAuthorization()
            .WithTags("Identity Resolution");

        group.MapGet("/rules", async (
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListRulesAsync(cancellationToken)));

        group.MapPost("/rules", async (
            CreateIdentityResolutionRuleRequest request,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateRuleAsync(request, cancellationToken)));

        group.MapPost("/batches/{batchId:guid}/candidates/generate", async (
            Guid batchId,
            GenerateIdentityCandidatesRequest request,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GenerateCandidatesAsync(batchId, request, cancellationToken)));

        group.MapGet("/batches/{batchId:guid}/candidates", async (
            Guid batchId,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListCandidatesAsync(batchId, cancellationToken)));

        group.MapPost("/candidates/{candidateId:guid}/approve", async (
            Guid candidateId,
            IdentityReviewDecisionRequest request,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ApproveCandidateAsync(candidateId, request, cancellationToken)));

        group.MapPost("/candidates/{candidateId:guid}/reject", async (
            Guid candidateId,
            IdentityReviewDecisionRequest request,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RejectCandidateAsync(candidateId, request, cancellationToken)));

        group.MapPost("/candidates/{candidateId:guid}/mark-conflicted", async (
            Guid candidateId,
            IdentityReviewDecisionRequest request,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkCandidateConflictedAsync(candidateId, request, cancellationToken)));

        group.MapGet("/batches/{batchId:guid}/trust-scores", async (
            Guid batchId,
            IIdentityResolutionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListTrustScoresAsync(batchId, cancellationToken)));

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
