namespace ETOS.Backend.ReviewTasks;

/// <summary>
/// Issue 20 hook: decision artifact creation from completed review tasks.
/// </summary>
public interface IReviewTaskCompletionHandler
{
    Task HandleCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        CancellationToken cancellationToken);
}

public sealed class DeferredReviewTaskCompletionHandler : IReviewTaskCompletionHandler
{
    public Task HandleCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
