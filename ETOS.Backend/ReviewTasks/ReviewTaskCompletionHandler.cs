namespace ETOS.Backend.ReviewTasks;

/// <summary>
/// Issue 20 hook: decision artifact creation from completed review tasks.
/// </summary>
public interface IReviewTaskCompletionHandler
{
    Task<DecisionCompletionHandleResult?> HandleCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        string? outcomeKey,
        string? summary,
        CancellationToken cancellationToken);
}

public sealed record DecisionCompletionHandleResult(
    Guid DecisionArtifactId,
    Guid DecisionVersionId);

public sealed class DeferredReviewTaskCompletionHandler : IReviewTaskCompletionHandler
{
    public Task<DecisionCompletionHandleResult?> HandleCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        string? outcomeKey,
        string? summary,
        CancellationToken cancellationToken)
        => Task.FromResult<DecisionCompletionHandleResult?>(null);
}
