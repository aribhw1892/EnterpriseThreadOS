using ETOS.Backend.Identity;

namespace ETOS.Backend.ReviewTasks;

public static class ReviewTaskEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadReviewTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/review-tasks")
            .RequireAuthorization()
            .WithTags("ReviewTasks");

        group.MapGet("/", async (
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapPost("/", async (
            CreateReviewTaskRequest request,
            IReviewTaskFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.CreateManualAsync(request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/from-recommendation/{artifactId:guid}/versions/{versionId:guid}/actions/{actionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            Guid actionId,
            IReviewTaskFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.FromRecommendationActionAsync(artifactId, versionId, actionId, cancellationToken)));

        group.MapPost("/from-data-quality-issue/{issueId:guid}", async (
            Guid issueId,
            IReviewTaskFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.FromDataQualityIssueAsync(issueId, cancellationToken)));

        group.MapPost("/from-security-event/{eventId:guid}", async (
            Guid eventId,
            IReviewTaskFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.FromSecurityEventAsync(eventId, cancellationToken)));

        group.MapPost("/from-access-request/{requestId:guid}", async (
            Guid requestId,
            IReviewTaskFactory factory,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => factory.FromAccessRequestAsync(requestId, cancellationToken)));

        group.MapPatch("/{artifactId:guid}/versions/{versionId:guid}/assign", async (
            Guid artifactId,
            Guid versionId,
            AssignReviewTaskRequest request,
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AssignAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPatch("/{artifactId:guid}/versions/{versionId:guid}/status", async (
            Guid artifactId,
            Guid versionId,
            UpdateReviewTaskStatusRequest request,
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateStatusAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/comments", async (
            Guid artifactId,
            Guid versionId,
            AddReviewTaskCommentRequest request,
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddCommentAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/complete", async (
            Guid artifactId,
            Guid versionId,
            CompleteReviewTaskRequest request,
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CompleteAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/escalation", async (
            Guid artifactId,
            Guid versionId,
            IReviewTaskService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateEscalationTaskAsync(artifactId, versionId, cancellationToken)));

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
