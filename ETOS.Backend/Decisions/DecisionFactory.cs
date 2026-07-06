using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.ReviewTasks;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Decisions;

public interface IDecisionFactory
{
    Task<ReviewTaskCompletionResult> CreateFromCompletedReviewTaskAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        string? outcomeKey,
        string? summary,
        CancellationToken cancellationToken);
}

public sealed class DecisionFactory(
    EnterpriseThreadDbContext dbContext,
    IDecisionConflictResolver conflictResolver,
    ILearningEvidenceEmitter learningEvidenceEmitter,
    ILearningSignalRollupService learningSignalRollupService) : IDecisionFactory
{
    public async Task<ReviewTaskCompletionResult> CreateFromCompletedReviewTaskAsync(
        Guid tenantId,
        Guid userId,
        Guid taskArtifactId,
        Guid taskVersionId,
        ReviewTaskCompletionResolution resolution,
        string? outcomeKey,
        string? summary,
        CancellationToken cancellationToken)
    {
        var taskVersion = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(version => version.Id == taskVersionId && version.ArtifactId == taskArtifactId, cancellationToken)
            ?? throw new RequestValidationException("Review task version was not found.");

        var taskArtifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(artifact => artifact.Id == taskArtifactId && artifact.TenantId == tenantId, cancellationToken)
            ?? throw new RequestValidationException("Review task artifact was not found.");

        var taskPayload = ReviewTaskPayloadParser.Deserialize(taskVersion.PayloadJson ?? "{}");
        var resolvedOutcomeKey = DecisionOutcomeKeyResolver.Resolve(resolution, outcomeKey);
        ValidateOutcomeKey(taskPayload, resolvedOutcomeKey);

        var approvalRule = await LoadApprovalRuleSnapshotAsync(tenantId, taskPayload.ReviewTemplateVersionId, cancellationToken);
        var decisionPayload = DecisionPayloadParser.CreateFromReviewTask(
            taskPayload,
            resolvedOutcomeKey,
            summary,
            summary,
            approvalRule,
            DecisionStatus.PendingVotes,
            DecisionConflictState.None);
        DecisionPayloadParser.ApplyReviewTaskIds(decisionPayload, taskArtifactId, taskVersionId);

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactType = DecisionArtifactTypes.Decision,
            NormalizedArtifactType = DecisionArtifactTypes.Decision.ToUpperInvariant(),
            Name = decisionPayload.Title ?? "Review decision",
            Description = $"Decision from review task {taskArtifactId}.",
            OwnerUserId = userId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = resolvedOutcomeKey,
            PayloadJson = DecisionPayloadParser.Serialize(decisionPayload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);

        var initialVote = new DecisionVote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DecisionArtifactId = artifact.Id,
            UserId = userId,
            Vote = DecisionOutcomeKeyResolver.ToVoteKind(resolution, resolvedOutcomeKey),
            Comment = summary,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.DecisionVotes.Add(initialVote);

        await AddRelationshipAsync(tenantId, artifact.Id, taskArtifactId, "Derived from completed review task.", cancellationToken);
        if (taskPayload.RecommendationArtifactId.HasValue)
        {
            await AddRelationshipAsync(tenantId, artifact.Id, taskPayload.RecommendationArtifactId.Value, "References recommendation.", cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var participantRoles = BuildParticipantRoles(taskPayload);
        var evaluation = conflictResolver.Evaluate(
            decisionPayload,
            [initialVote],
            participantRoles);
        await ApplyEvaluationAsync(decisionPayload, version, artifact, evaluation, userId, cancellationToken);

        await learningEvidenceEmitter.EmitDecisionEvidenceAsync(
            tenantId,
            artifact.Id,
            decisionPayload,
            evaluation.IsFinalized,
            cancellationToken);
        await learningSignalRollupService.EvaluateAsync(tenantId, userId, decisionPayload, cancellationToken);

        return new ReviewTaskCompletionResult(artifact.Id, version.Id, decisionPayload.Status);
    }

    private async Task ApplyEvaluationAsync(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        ArtifactVersion version,
        Artifact artifact,
        DecisionConflictEvaluation evaluation,
        Guid userId,
        CancellationToken cancellationToken)
    {
        payload.Status = evaluation.Status;
        payload.OutcomeKey = evaluation.OutcomeKey;
        payload.OutcomeSummary = evaluation.OutcomeSummary;
        payload.ConflictState = evaluation.ConflictState;
        if (evaluation.IsFinalized)
        {
            payload.FinalizedAt = DateTimeOffset.UtcNow;
            payload.FinalizedByUserId = userId;
        }

        version.PayloadJson = DecisionPayloadParser.Serialize(payload);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DecisionPayloadParser.DecisionApprovalRuleSnapshotDocument> LoadApprovalRuleSnapshotAsync(
        Guid tenantId,
        Guid? templateVersionId,
        CancellationToken cancellationToken)
    {
        if (!templateVersionId.HasValue)
        {
            return DecisionPayloadParser.DefaultApprovalRule();
        }

        var templateVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(version => version.Id == templateVersionId.Value && version.TenantId == tenantId, cancellationToken);

        if (templateVersion?.PayloadJson is null)
        {
            return DecisionPayloadParser.DefaultApprovalRule();
        }

        var template = ReviewTaskTemplatePayloadParser.Deserialize(templateVersion.PayloadJson);
        return DecisionPayloadParser.FromTemplateApprovalRule(template.ApprovalRule);
    }

    private static void ValidateOutcomeKey(
        ReviewTaskPayloadParser.ReviewTaskPayloadDocument taskPayload,
        string outcomeKey)
    {
        if (taskPayload.ReviewTemplateVersionId is null)
        {
            return;
        }
    }

    private static Dictionary<Guid, ReviewTaskParticipantRole> BuildParticipantRoles(
        ReviewTaskPayloadParser.ReviewTaskPayloadDocument taskPayload)
    {
        var roles = new Dictionary<Guid, ReviewTaskParticipantRole>();
        if (taskPayload.PrimaryOwnerUserId.HasValue)
        {
            roles[taskPayload.PrimaryOwnerUserId.Value] = ReviewTaskParticipantRole.PrimaryOwner;
        }

        foreach (var participant in taskPayload.Participants ?? [])
        {
            roles[participant.UserId] = participant.Role;
        }

        return roles;
    }

    private async Task AddRelationshipAsync(
        Guid tenantId,
        Guid sourceArtifactId,
        Guid targetArtifactId,
        string description,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ArtifactRelationships.AnyAsync(
            relationship => relationship.TenantId == tenantId
                && relationship.SourceArtifactId == sourceArtifactId
                && relationship.TargetArtifactId == targetArtifactId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.ArtifactRelationships.Add(new ArtifactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceArtifactId = sourceArtifactId,
            TargetArtifactId = targetArtifactId,
            RelationshipType = ArtifactRelationshipType.DerivedFrom,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}

internal static class DecisionOutcomeKeyResolver
{
    public static string Resolve(ReviewTaskCompletionResolution resolution, string? requestedKey)
    {
        if (!string.IsNullOrWhiteSpace(requestedKey))
        {
            return requestedKey.Trim();
        }

        return resolution == ReviewTaskCompletionResolution.Accepted ? "accept" : "reject";
    }

    public static DecisionVoteKind ToVoteKind(ReviewTaskCompletionResolution resolution, string outcomeKey)
    {
        if (outcomeKey.Equals("accept", StringComparison.OrdinalIgnoreCase)
            || outcomeKey.Equals("approved", StringComparison.OrdinalIgnoreCase))
        {
            return DecisionVoteKind.Approve;
        }

        if (outcomeKey.Equals("reject", StringComparison.OrdinalIgnoreCase)
            || outcomeKey.Equals("rejected", StringComparison.OrdinalIgnoreCase))
        {
            return DecisionVoteKind.Reject;
        }

        return resolution == ReviewTaskCompletionResolution.Accepted
            ? DecisionVoteKind.Abstain
            : DecisionVoteKind.Dissent;
    }
}
