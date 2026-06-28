using ETOS.Backend.Artifacts;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ReviewTasks;

public interface IReviewTaskChainService
{
    Task<ReviewTaskChainLink> CreateChainLinkAsync(
        Guid tenantId,
        Guid userId,
        Guid blockedTaskArtifactId,
        Guid blockingTaskArtifactId,
        ReviewTaskChainReason chainReason,
        ReviewTaskBlockingCondition blockingCondition,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> HandlePrerequisiteCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid completedTaskArtifactId,
        ReviewTaskCompletionResolution resolution,
        CancellationToken cancellationToken);
}

public sealed class ReviewTaskChainService(
    EnterpriseThreadDbContext dbContext,
    IAuditRecorder auditRecorder) : IReviewTaskChainService
{
    public async Task<ReviewTaskChainLink> CreateChainLinkAsync(
        Guid tenantId,
        Guid userId,
        Guid blockedTaskArtifactId,
        Guid blockingTaskArtifactId,
        ReviewTaskChainReason chainReason,
        ReviewTaskBlockingCondition blockingCondition,
        CancellationToken cancellationToken)
    {
        if (blockedTaskArtifactId == blockingTaskArtifactId)
        {
            throw new RequestValidationException("A review task cannot block itself.");
        }

        var link = new ReviewTaskChainLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BlockedTaskArtifactId = blockedTaskArtifactId,
            BlockingTaskArtifactId = blockingTaskArtifactId,
            ChainReason = chainReason,
            BlockingCondition = blockingCondition,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ReviewTaskChainLinks.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                tenantId,
                userId,
                "review-tasks.chain.create",
                AuditResult.Success,
                null,
                $"Review task '{blockedTaskArtifactId}' blocked by '{blockingTaskArtifactId}'.",
                nameof(ReviewTaskChainLink),
                link.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return link;
    }

    public async Task<IReadOnlyCollection<Guid>> HandlePrerequisiteCompletedAsync(
        Guid tenantId,
        Guid userId,
        Guid completedTaskArtifactId,
        ReviewTaskCompletionResolution resolution,
        CancellationToken cancellationToken)
    {
        var links = await dbContext.ReviewTaskChainLinks
            .Where(link => link.TenantId == tenantId
                && link.BlockingTaskArtifactId == completedTaskArtifactId
                && link.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var unblocked = new List<Guid>();
        foreach (var link in links)
        {
            var blockedArtifact = await dbContext.Artifacts
                .SingleOrDefaultAsync(item => item.Id == link.BlockedTaskArtifactId && item.TenantId == tenantId, cancellationToken);
            if (blockedArtifact is null)
            {
                continue;
            }

            var blockedVersion = await dbContext.ArtifactVersions
                .Where(version => version.ArtifactId == blockedArtifact.Id && version.TenantId == tenantId)
                .OrderByDescending(version => version.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (blockedVersion?.PayloadJson is null)
            {
                continue;
            }

            var payload = ReviewTaskPayloadParser.Deserialize(blockedVersion.PayloadJson);
            if (payload.Status != ReviewTaskStatus.Blocked)
            {
                continue;
            }

            if (resolution == ReviewTaskCompletionResolution.Accepted
                && link.BlockingCondition == ReviewTaskBlockingCondition.PrerequisiteAccepted)
            {
                payload.Status = ReviewTaskStatus.Open;
                payload.BlockingReason = null;
                payload.PrerequisiteTaskIds = payload.PrerequisiteTaskIds?
                    .Where(id => id != completedTaskArtifactId)
                    .ToList() ?? [];
                blockedVersion.PayloadJson = ReviewTaskPayloadParser.Serialize(payload);
                blockedArtifact.UpdatedAt = DateTimeOffset.UtcNow;
                link.ResolvedAt = DateTimeOffset.UtcNow;
                unblocked.Add(blockedArtifact.Id);

                await auditRecorder.RecordAsync(
                    new AuditRecordWriteRequest(
                        tenantId,
                        userId,
                        "review-tasks.chain.unblock",
                        AuditResult.Success,
                        null,
                        $"Review task '{blockedArtifact.Id}' unblocked after prerequisite '{completedTaskArtifactId}' accepted.",
                        nameof(Artifact),
                        blockedArtifact.Id.ToString(),
                        RetentionCategory: AuditRetentionCategory.Operational),
                    cancellationToken);
            }
            else if (resolution == ReviewTaskCompletionResolution.Rejected)
            {
                payload.Status = ReviewTaskStatus.NeedsReevaluation;
                payload.BlockingReason = $"Prerequisite task '{completedTaskArtifactId}' was rejected.";
                blockedVersion.PayloadJson = ReviewTaskPayloadParser.Serialize(payload);
                blockedArtifact.UpdatedAt = DateTimeOffset.UtcNow;
                link.ResolvedAt = DateTimeOffset.UtcNow;

                await auditRecorder.RecordAsync(
                    new AuditRecordWriteRequest(
                        tenantId,
                        userId,
                        "review-tasks.chain.reevaluate",
                        AuditResult.Success,
                        null,
                        $"Review task '{blockedArtifact.Id}' marked needs reevaluation after prerequisite rejection.",
                        nameof(Artifact),
                        blockedArtifact.Id.ToString(),
                        RetentionCategory: AuditRetentionCategory.Operational),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return unblocked;
    }
}
