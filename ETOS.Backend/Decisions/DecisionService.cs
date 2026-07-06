using ETOS.Backend.Artifacts;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.GovernanceAnalytics;
using ETOS.Backend.ReviewTasks;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Decisions;

public interface IDecisionService
{
    Task<IReadOnlyCollection<DecisionSummaryResponse>> ListAsync(
        string? status,
        string? conflict,
        string? outcomeKey,
        CancellationToken cancellationToken);

    Task<DecisionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);

    Task<CastDecisionVoteResponse> CastVoteAsync(
        Guid artifactId,
        Guid versionId,
        CastDecisionVoteRequest request,
        CancellationToken cancellationToken);

    Task<AddDecisionCommentResponse> AddCommentAsync(
        Guid artifactId,
        Guid versionId,
        AddDecisionCommentRequest request,
        CancellationToken cancellationToken);

    Task<FinalizeDecisionResponse> FinalizeAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken);

    Task<CreateDecisionEscalationResponse> CreateEscalationAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken);
}

public sealed class DecisionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IDecisionConflictResolver conflictResolver,
    ILearningEvidenceEmitter learningEvidenceEmitter,
    ILearningSignalRollupService learningSignalRollupService) : IDecisionService
{
    public async Task<IReadOnlyCollection<DecisionSummaryResponse>> ListAsync(
        string? status,
        string? conflict,
        string? outcomeKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("decisions.list", DecisionPermissions.Read, cancellationToken);
        var normalizedType = DecisionArtifactTypes.Decision.ToUpperInvariant();

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId && artifact.NormalizedArtifactType == normalizedType)
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);
        var outcomeDecisionIds = await DecisionExplorerQueryHelper.LoadDecisionIdsWithOutcomeChecksAsync(
            dbContext,
            context.TenantId,
            artifactIds,
            cancellationToken);

        var responses = new List<DecisionSummaryResponse>();
        foreach (var artifact in artifacts)
        {
            if (!versionLookup.TryGetValue(artifact.Id, out var version))
            {
                continue;
            }

            var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            var filter = new DecisionExplorerFilter(status, null, null, conflict, outcomeKey);
            if (!DecisionExplorerQueryHelper.MatchesFilter(
                    payload,
                    artifact.Name,
                    filter,
                    outcomeDecisionIds.Contains(artifact.Id)))
            {
                continue;
            }

            responses.Add(ToSummary(artifact, payload));
        }

        return responses;
    }

    public async Task<DecisionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "decisions.get", DecisionPermissions.Read, cancellationToken);
        return await BuildDetailAsync(artifact, version, cancellationToken);
    }

    public async Task<CastDecisionVoteResponse> CastVoteAsync(
        Guid artifactId,
        Guid versionId,
        CastDecisionVoteRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("decisions.vote", DecisionPermissions.Vote, cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "decisions.vote", DecisionPermissions.Vote, cancellationToken);
        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");

        if (payload.Status is DecisionStatus.Finalized or DecisionStatus.Superseded)
        {
            throw new RequestValidationException("Decision is already closed.");
        }

        var existingVote = await dbContext.DecisionVotes
            .SingleOrDefaultAsync(
                vote => vote.TenantId == context.TenantId
                    && vote.DecisionArtifactId == artifactId
                    && vote.UserId == context.UserId,
                cancellationToken);

        if (existingVote is null)
        {
            existingVote = new DecisionVote
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                DecisionArtifactId = artifactId,
                UserId = context.UserId,
                Vote = request.Vote,
                Comment = TrimOptional(request.Comment),
                Confidence = request.Confidence,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.DecisionVotes.Add(existingVote);
        }
        else
        {
            existingVote.Vote = request.Vote;
            existingVote.Comment = TrimOptional(request.Comment);
            existingVote.Confidence = request.Confidence;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var participantRoles = await LoadParticipantRolesAsync(payload, cancellationToken);
        var votes = await dbContext.DecisionVotes
            .AsNoTracking()
            .Where(vote => vote.TenantId == context.TenantId && vote.DecisionArtifactId == artifactId)
            .ToListAsync(cancellationToken);
        var evaluation = conflictResolver.Evaluate(payload, votes, participantRoles);
        await ApplyEvaluationAsync(payload, version, artifact, evaluation, context.UserId, cancellationToken);

        if (evaluation.IsFinalized)
        {
            await learningEvidenceEmitter.EmitDecisionEvidenceAsync(
                context.TenantId,
                artifactId,
                payload,
                true,
                cancellationToken);
            await learningSignalRollupService.EvaluateAsync(context.TenantId, context.UserId, payload, cancellationToken);
        }

        return new CastDecisionVoteResponse(
            existingVote.Id,
            artifactId,
            versionId,
            payload.Status,
            payload.ConflictState,
            ToVoteResponse(existingVote));
    }

    public async Task<AddDecisionCommentResponse> AddCommentAsync(
        Guid artifactId,
        Guid versionId,
        AddDecisionCommentRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("decisions.comment", DecisionPermissions.Read, cancellationToken);
        await RequireVersionAsync(artifactId, versionId, "decisions.comment", DecisionPermissions.Read, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new RequestValidationException("Comment body is required.");
        }

        var comment = new DecisionComment
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DecisionArtifactId = artifactId,
            AuthorUserId = context.UserId,
            Body = request.Body.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.DecisionComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddDecisionCommentResponse(
            comment.Id,
            artifactId,
            versionId,
            new DecisionCommentResponse(comment.Id, comment.AuthorUserId, comment.Body, comment.CreatedAt));
    }

    public async Task<FinalizeDecisionResponse> FinalizeAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("decisions.finalize", DecisionPermissions.Manage, cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "decisions.finalize", DecisionPermissions.Manage, cancellationToken);
        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");

        if (payload.Status == DecisionStatus.Finalized)
        {
            return new FinalizeDecisionResponse(
                artifactId,
                versionId,
                payload.Status,
                payload.OutcomeKey ?? string.Empty,
                payload.OutcomeSummary ?? string.Empty,
                payload.ConflictState);
        }

        var participantRoles = await LoadParticipantRolesAsync(payload, cancellationToken);
        var votes = await dbContext.DecisionVotes
            .AsNoTracking()
            .Where(vote => vote.TenantId == context.TenantId && vote.DecisionArtifactId == artifactId)
            .ToListAsync(cancellationToken);
        var evaluation = conflictResolver.Evaluate(payload, votes, participantRoles);
        if (!evaluation.IsFinalized)
        {
            throw new RequestValidationException("Decision cannot be finalized until approval rules are satisfied or conflict is resolved.");
        }

        await ApplyEvaluationAsync(payload, version, artifact, evaluation, context.UserId, cancellationToken);
        await learningEvidenceEmitter.EmitDecisionEvidenceAsync(context.TenantId, artifactId, payload, true, cancellationToken);
        await learningSignalRollupService.EvaluateAsync(context.TenantId, context.UserId, payload, cancellationToken);

        return new FinalizeDecisionResponse(
            artifactId,
            versionId,
            payload.Status,
            payload.OutcomeKey ?? string.Empty,
            payload.OutcomeSummary ?? string.Empty,
            payload.ConflictState);
    }

    public async Task<CreateDecisionEscalationResponse> CreateEscalationAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("decisions.escalation.create", DecisionPermissions.Manage, cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "decisions.escalation.create", DecisionPermissions.Manage, cancellationToken);
        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");

        if (payload.Status != DecisionStatus.BlockedConflict)
        {
            throw new RequestValidationException("Escalation is only available for blocked/conflict decisions.");
        }

        if (!payload.ReviewTaskArtifactId.HasValue || !payload.ReviewTaskVersionId.HasValue)
        {
            throw new RequestValidationException("Decision has no linked review task.");
        }

        var taskVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == payload.ReviewTaskVersionId.Value, cancellationToken)
            ?? throw new RequestValidationException("Linked review task version was not found.");

        var taskPayload = ReviewTaskPayloadParser.Deserialize(taskVersion.PayloadJson ?? "{}");
        if (taskPayload.EscalationPlaceholder?.Enabled != true || !taskPayload.ReviewTemplateVersionId.HasValue)
        {
            throw new RequestValidationException("Escalation path is not enabled for the linked review task template.");
        }

        var templateVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == taskPayload.ReviewTemplateVersionId.Value, cancellationToken)
            ?? throw new RequestValidationException("Review task template version was not found.");

        var templatePayload = ReviewTaskTemplatePayloadParser.Deserialize(templateVersion.PayloadJson ?? "{}");
        if (templatePayload.EscalationPath?.Enabled != true)
        {
            throw new RequestValidationException("Template escalation path is not enabled.");
        }

        var escalationPayload = ReviewTaskPayloadParser.CreateDefault(
            $"Escalation for decision {artifactId}",
            taskPayload.SourceType,
            $"{taskPayload.SourceReference}:decision-escalation",
            taskPayload.ReviewTaskType,
            null,
            templatePayload.EscalationPath.EscalationTargetRoleKey,
            null,
            ReviewTaskPriority.High,
            taskPayload.Severity,
            taskPayload.TrustState,
            taskPayload.ConflictState,
            taskPayload.ConfidenceScore,
            taskPayload.EvidenceReferences,
            taskPayload.ReviewTemplateVersionId,
            taskPayload.RecommendationArtifactId,
            taskPayload.RecommendationVersionId,
            taskPayload.SuggestedActionId,
            taskPayload.DataQualityIssueId,
            taskPayload.SecurityEventId,
            taskPayload.AccessRequestId,
            taskPayload.AiTraceId,
            taskPayload.ContextPackageId,
            taskPayload.DueDate,
            taskPayload.EscalationPlaceholder,
            ReviewTaskStatus.Open);

        var escalationArtifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskArtifactTypes.ReviewTask,
            NormalizedArtifactType = ReviewTaskArtifactTypes.ReviewTask.ToUpperInvariant(),
            Name = escalationPayload.Title!,
            Description = $"Escalation task for blocked decision '{artifact.Name}'.",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var escalationVersion = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = escalationArtifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = escalationPayload.Title,
            PayloadJson = ReviewTaskPayloadParser.Serialize(escalationPayload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        payload.Status = DecisionStatus.Escalated;
        version.PayloadJson = DecisionPayloadParser.Serialize(payload);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.Artifacts.Add(escalationArtifact);
        dbContext.ArtifactVersions.Add(escalationVersion);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "decisions.escalation.create",
                AuditResult.Success,
                null,
                $"Escalation review task '{escalationArtifact.Id}' created from blocked decision '{artifactId}'.",
                nameof(Artifact),
                artifactId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateDecisionEscalationResponse(
            escalationArtifact.Id,
            escalationVersion.Id,
            artifactId,
            versionId);
    }

    private async Task<DecisionDetailResponse> BuildDetailAsync(
        Artifact artifact,
        ArtifactVersion version,
        CancellationToken cancellationToken)
    {
        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var votes = await dbContext.DecisionVotes
            .AsNoTracking()
            .Where(vote => vote.TenantId == artifact.TenantId && vote.DecisionArtifactId == artifact.Id)
            .OrderBy(vote => vote.CreatedAt)
            .ToListAsync(cancellationToken);
        var comments = await dbContext.DecisionComments
            .AsNoTracking()
            .Where(comment => comment.TenantId == artifact.TenantId && comment.DecisionArtifactId == artifact.Id)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

        var approvalRule = payload.ApprovalRuleSnapshot ?? DecisionPayloadParser.DefaultApprovalRule();
        return new DecisionDetailResponse(
            artifact.Id,
            version.Id,
            version.VersionLabel,
            artifact.Name,
            payload.Title ?? artifact.Name,
            payload.Status,
            payload.OutcomeKey ?? string.Empty,
            payload.OutcomeSummary ?? string.Empty,
            payload.DecisionReason,
            payload.ConflictState,
            payload.ReviewTaskArtifactId ?? Guid.Empty,
            payload.ReviewTaskVersionId ?? Guid.Empty,
            payload.ReviewTemplateVersionId,
            payload.RecommendationArtifactId,
            payload.DataQualityIssueId,
            payload.SecurityEventId,
            payload.AccessRequestId,
            payload.AiTraceId,
            payload.ContextPackageId,
            payload.ParentDecisionArtifactId,
            new DecisionApprovalRuleSnapshotResponse(
                approvalRule.Mode,
                approvalRule.RequiredRoles ?? [],
                approvalRule.OutcomeTaxonomyVersionId,
                approvalRule.OutcomeTrackingRequired),
            payload.ParticipantUserIds ?? [],
            payload.EvidenceReferences?.Select(item => new DecisionEvidenceReferenceResponse(
                item.LinkId,
                item.EvidenceType,
                item.SourceId,
                item.SafeSummary,
                item.TrustState)).ToList() ?? [],
            payload.OutcomeTrackingRequired,
            payload.OutcomeTaxonomyVersionId,
            payload.FinalizedAt,
            payload.FinalizedByUserId,
            votes.Select(ToVoteResponse).ToList(),
            comments.Select(comment => new DecisionCommentResponse(
                comment.Id,
                comment.AuthorUserId,
                comment.Body,
                comment.CreatedAt)).ToList(),
            $"/artifacts/{artifact.Id}");
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

    private async Task<Dictionary<Guid, ReviewTaskParticipantRole>> LoadParticipantRolesAsync(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        if (!payload.ReviewTaskVersionId.HasValue)
        {
            return [];
        }

        var taskVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(version => version.Id == payload.ReviewTaskVersionId.Value, cancellationToken);
        if (taskVersion?.PayloadJson is null)
        {
            return [];
        }

        var taskPayload = ReviewTaskPayloadParser.Deserialize(taskVersion.PayloadJson);
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

    private static DecisionSummaryResponse ToSummary(Artifact artifact, DecisionPayloadParser.DecisionPayloadDocument payload)
        => new(
            artifact.Id,
            artifact.ArtifactType,
            payload.Title ?? artifact.Name,
            payload.Status.ToString(),
            payload.OutcomeKey ?? string.Empty,
            payload.ParticipantUserIds?.Select(id => id.ToString()).ToList() ?? [],
            payload.EvidenceReferences?.Count ?? 0,
            payload.ConflictState.ToString(),
            payload.OutcomeSummary ?? string.Empty,
            $"/decisions/{artifact.Id}");

    private static DecisionVoteResponse ToVoteResponse(DecisionVote vote)
        => new(vote.Id, vote.UserId, vote.Vote, vote.Comment, vote.Confidence, vote.CreatedAt);

    private async Task<(Artifact Artifact, ArtifactVersion Version)> RequireVersionAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        string permission,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(action, permission, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_mismatch", "The requested record belongs to a different tenant.", cancellationToken);
            throw new TenantAccessDeniedException("The requested record belongs to a different tenant.");
        }

        if (!artifact.ArtifactType.Equals(DecisionArtifactTypes.Decision, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a decision.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        return (artifact, version);
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permission,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permission, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DecisionPermissions.Admin, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "missing_permission", "Decision permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Decision permission is required.");
        }

        return context;
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
