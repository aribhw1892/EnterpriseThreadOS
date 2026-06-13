using ETOS.Backend.Identity;

namespace ETOS.Backend.Recommendations;

public static class RecommendationEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadRecommendationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/recommendations")
            .RequireAuthorization()
            .WithTags("Recommendations");

        group.MapGet("/", async (
            IRecommendationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IRecommendationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/", async (
            CreateRecommendationRequest request,
            IRecommendationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAsync(request, cancellationToken)));

        group.MapPost("/from-data-quality-issue/{issueId:guid}", async (
            Guid issueId,
            IRecommendationFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.FromDataQualityIssueAsync(issueId, cancellationToken)));

        group.MapPost("/from-bom-comparison/{runId:guid}", async (
            Guid runId,
            IRecommendationFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.FromBomComparisonRunAsync(runId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-reviewed", async (
            Guid artifactId,
            Guid versionId,
            IRecommendationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReviewedAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IRecommendationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(artifactId, versionId, cancellationToken)));

        group.MapPatch("/{artifactId:guid}/versions/{versionId:guid}/suggested-actions/{actionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            Guid actionId,
            UpdateSuggestedActionStatusRequest request,
            IRecommendationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateSuggestedActionStatusAsync(artifactId, versionId, actionId, request, cancellationToken)));

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
