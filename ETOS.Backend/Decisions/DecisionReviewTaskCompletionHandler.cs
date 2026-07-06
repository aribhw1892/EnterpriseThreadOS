using ETOS.Backend.ReviewTasks;

namespace ETOS.Backend.Decisions;

public sealed class DecisionReviewTaskCompletionHandler(IDecisionFactory decisionFactory) : IReviewTaskCompletionHandler
{
    public async Task<DecisionCompletionHandleResult?> HandleCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        string? outcomeKey,
        string? summary,
        CancellationToken cancellationToken)
    {
        var result = await decisionFactory.CreateFromCompletedReviewTaskAsync(
            tenantId,
            userId,
            taskArtifactId,
            taskVersionId,
            resolution,
            outcomeKey,
            summary,
            cancellationToken);
        return new DecisionCompletionHandleResult(result.DecisionArtifactId, result.DecisionVersionId);
    }
}
