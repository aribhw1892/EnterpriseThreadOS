using ETOS.Backend.Artifacts;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ReviewTasks;

public interface IReviewTaskService
{
    Task<IReadOnlyCollection<ReviewTaskArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ReviewTaskPayloadResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<AssignReviewTaskResponse> AssignAsync(
        Guid artifactId,
        Guid versionId,
        AssignReviewTaskRequest request,
        CancellationToken cancellationToken);
    Task<UpdateReviewTaskStatusResponse> UpdateStatusAsync(
        Guid artifactId,
        Guid versionId,
        UpdateReviewTaskStatusRequest request,
        CancellationToken cancellationToken);
    Task<AddReviewTaskCommentResponse> AddCommentAsync(
        Guid artifactId,
        Guid versionId,
        AddReviewTaskCommentRequest request,
        CancellationToken cancellationToken);
    Task<CompleteReviewTaskResponse> CompleteAsync(
        Guid artifactId,
        Guid versionId,
        CompleteReviewTaskRequest request,
        CancellationToken cancellationToken);
    Task<CreateEscalationReviewTaskResponse> CreateEscalationTaskAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken);
}

public sealed class ReviewTaskService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IReviewTaskChainService chainService,
    IReviewTaskCompletionHandler completionHandler) : IReviewTaskService
{
    public async Task<IReadOnlyCollection<ReviewTaskArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("review-tasks.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("review-tasks.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == ReviewTaskArtifactTypes.ReviewTask.ToUpperInvariant())
            .OrderByDescending(item => item.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(item => item.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        return artifacts.Select(artifact =>
        {
            versionLookup.TryGetValue(artifact.Id, out var version);
            ReviewTaskStatus? status = null;
            ReviewTaskPriority? priority = null;
            Guid? primaryOwnerUserId = null;
            ReviewTaskSourceType? sourceType = null;
            var isBlocked = false;

            if (version?.PayloadJson is not null)
            {
                var payload = ReviewTaskPayloadParser.Deserialize(version.PayloadJson);
                status = payload.Status;
                priority = payload.Priority;
                primaryOwnerUserId = payload.PrimaryOwnerUserId;
                sourceType = payload.SourceType;
                isBlocked = payload.Status == ReviewTaskStatus.Blocked;
            }

            return new ReviewTaskArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                status,
                priority,
                primaryOwnerUserId,
                sourceType,
                isBlocked,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<ReviewTaskPayloadResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-tasks.get", cancellationToken);
        var chainLinks = await LoadChainLinksAsync(artifact.TenantId, artifact.Id, cancellationToken);
        var comments = await LoadCommentsAsync(artifact.TenantId, artifact.Id, cancellationToken);

        return ReviewTaskPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            chainLinks,
            comments);
    }

    public async Task<AssignReviewTaskResponse> AssignAsync(
        Guid artifactId,
        Guid versionId,
        AssignReviewTaskRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireAssignPermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-tasks.assign", cancellationToken);
        await ReviewTaskMembershipValidator.ValidateAssigneesAsync(
            dbContext,
            context.TenantId,
            request.PrimaryOwnerUserId,
            request.Participants,
            cancellationToken);

        var payload = ReviewTaskPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        payload.PrimaryOwnerUserId = request.PrimaryOwnerUserId;
        payload.AssignedRoleKey = request.AssignedRoleKey?.Trim();
        payload.Participants = request.Participants?.Select(item => new ReviewTaskPayloadParser.ReviewTaskParticipantDocument
        {
            UserId = item.UserId,
            Role = item.Role
        }).ToList() ?? payload.Participants;

        version.PayloadJson = ReviewTaskPayloadParser.Serialize(payload);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "review-tasks.assign",
                AuditResult.Success,
                null,
                $"Review task '{artifactId}' assigned.",
                nameof(Artifact),
                artifactId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new AssignReviewTaskResponse(
            artifactId,
            versionId,
            payload.PrimaryOwnerUserId,
            payload.AssignedRoleKey,
            payload.Participants?.Select(item => new ReviewTaskParticipantResponse(item.UserId, item.Role)).ToList() ?? []);
    }

    public async Task<UpdateReviewTaskStatusResponse> UpdateStatusAsync(
        Guid artifactId,
        Guid versionId,
        UpdateReviewTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireManagePermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-tasks.status.update", cancellationToken);

        var payload = ReviewTaskPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        payload.Status = request.Status;
        payload.BlockingReason = request.BlockingReason?.Trim();

        version.PayloadJson = ReviewTaskPayloadParser.Serialize(payload);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "review-tasks.status.update",
                AuditResult.Success,
                null,
                $"Review task '{artifactId}' status updated to {request.Status}.",
                nameof(Artifact),
                artifactId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new UpdateReviewTaskStatusResponse(artifactId, versionId, request.Status, payload.BlockingReason);
    }

    public async Task<AddReviewTaskCommentResponse> AddCommentAsync(
        Guid artifactId,
        Guid versionId,
        AddReviewTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireManagePermissionAsync(cancellationToken);
        await RequireVersionAsync(artifactId, versionId, "review-tasks.comments.add", cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new RequestValidationException("Comment body is required.");
        }

        var comment = new ReviewTaskComment
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            TaskArtifactId = artifactId,
            AuthorUserId = context.UserId,
            Body = request.Body.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ReviewTaskComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new ReviewTaskCommentResponse(comment.Id, comment.AuthorUserId, comment.Body, comment.CreatedAt);
        return new AddReviewTaskCommentResponse(comment.Id, artifactId, versionId, response);
    }

    public async Task<CompleteReviewTaskResponse> CompleteAsync(
        Guid artifactId,
        Guid versionId,
        CompleteReviewTaskRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireManagePermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-tasks.complete", cancellationToken);

        var payload = ReviewTaskPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        if (payload.Status is ReviewTaskStatus.Completed or ReviewTaskStatus.Cancelled)
        {
            throw new RequestValidationException("Review task is already closed.");
        }

        payload.Status = ReviewTaskStatus.Completed;
        payload.BlockingReason = null;
        version.PayloadJson = ReviewTaskPayloadParser.Serialize(payload);
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var unblocked = await chainService.HandlePrerequisiteCompletedAsync(
            context.TenantId,
            context.UserId,
            artifactId,
            request.Resolution,
            cancellationToken);

        await completionHandler.HandleCompletedAsync(
            context.TenantId,
            context.UserId,
            artifactId,
            versionId,
            request.Resolution,
            cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "review-tasks.complete",
                AuditResult.Success,
                null,
                $"Review task '{artifactId}' completed with resolution {request.Resolution}.",
                nameof(Artifact),
                artifactId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CompleteReviewTaskResponse(
            artifactId,
            versionId,
            ReviewTaskStatus.Completed,
            DecisionCreationDeferred: true,
            unblocked);
    }

    public async Task<CreateEscalationReviewTaskResponse> CreateEscalationTaskAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireManagePermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "review-tasks.escalation.create", cancellationToken);
        var payload = ReviewTaskPayloadParser.Deserialize(version.PayloadJson ?? "{}");

        if (payload.EscalationPlaceholder?.Enabled != true)
        {
            throw new RequestValidationException("Escalation path is not enabled for this review task template.");
        }

        if (payload.ReviewTemplateVersionId is not Guid templateVersionId)
        {
            throw new RequestValidationException("Review task has no linked template version.");
        }

        var templateVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken)
            ?? throw new RequestValidationException("Review task template version was not found.");

        var templatePayload = ReviewTaskTemplatePayloadParser.Deserialize(templateVersion.PayloadJson ?? "{}");
        if (templatePayload.EscalationPath?.Enabled != true)
        {
            throw new RequestValidationException("Template escalation path is not enabled.");
        }

        var escalationPayload = ReviewTaskPayloadParser.CreateDefault(
            $"Escalation for {payload.Title}",
            payload.SourceType,
            $"{payload.SourceReference}:escalation",
            payload.ReviewTaskType,
            null,
            templatePayload.EscalationPath.EscalationTargetRoleKey,
            null,
            ReviewTaskPriority.High,
            payload.Severity,
            payload.TrustState,
            payload.ConflictState,
            payload.ConfidenceScore,
            payload.EvidenceReferences,
            templateVersionId,
            payload.RecommendationArtifactId,
            payload.RecommendationVersionId,
            payload.SuggestedActionId,
            payload.DataQualityIssueId,
            payload.SecurityEventId,
            payload.AccessRequestId,
            payload.AiTraceId,
            payload.ContextPackageId,
            payload.DueDate,
            payload.EscalationPlaceholder,
            ReviewTaskStatus.Open);

        var escalationArtifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskArtifactTypes.ReviewTask,
            NormalizedArtifactType = ReviewTaskArtifactTypes.ReviewTask.ToUpperInvariant(),
            Name = escalationPayload.Title!,
            Description = $"Escalation task for '{artifact.Name}'.",
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

        dbContext.Artifacts.Add(escalationArtifact);
        dbContext.ArtifactVersions.Add(escalationVersion);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "review-tasks.escalation.create",
                AuditResult.Success,
                null,
                $"Escalation review task '{escalationArtifact.Id}' created from '{artifactId}'.",
                nameof(Artifact),
                escalationArtifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateEscalationReviewTaskResponse(
            escalationArtifact.Id,
            escalationVersion.Id,
            escalationVersion.VersionLabel,
            ReviewTaskStatus.Open);
    }

    private async Task<IReadOnlyCollection<ReviewTaskChainLinkResponse>> LoadChainLinksAsync(
        Guid tenantId,
        Guid taskArtifactId,
        CancellationToken cancellationToken)
    {
        var links = await dbContext.ReviewTaskChainLinks
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId
                && (link.BlockedTaskArtifactId == taskArtifactId || link.BlockingTaskArtifactId == taskArtifactId))
            .OrderByDescending(link => link.CreatedAt)
            .ToListAsync(cancellationToken);

        return links.Select(link => new ReviewTaskChainLinkResponse(
            link.Id,
            link.BlockedTaskArtifactId,
            link.BlockingTaskArtifactId,
            link.ChainReason,
            link.BlockingCondition,
            link.CreatedAt,
            link.ResolvedAt)).ToList();
    }

    private async Task<IReadOnlyCollection<ReviewTaskCommentResponse>> LoadCommentsAsync(
        Guid tenantId,
        Guid taskArtifactId,
        CancellationToken cancellationToken)
    {
        var comments = await dbContext.ReviewTaskComments
            .AsNoTracking()
            .Where(comment => comment.TenantId == tenantId && comment.TaskArtifactId == taskArtifactId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

        return comments.Select(comment => new ReviewTaskCommentResponse(
            comment.Id,
            comment.AuthorUserId,
            comment.Body,
            comment.CreatedAt)).ToList();
    }

    private async Task<(Artifact Artifact, ArtifactVersion Version)> RequireVersionAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync(action, cancellationToken);
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        if (!artifact.ArtifactType.Equals(ReviewTaskArtifactTypes.ReviewTask, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a review task.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        if (version.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        return (artifact, version);
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskPermissions.Read, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "missing_permission", "Review task read permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Review task read permission is required.");
        }
    }

    private async Task<ActiveTenantContext> RequireAssignPermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("review-tasks.assign", cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskPermissions.Assign, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, "review-tasks.assign", "missing_permission", "Review task assign permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Review task assign permission is required.");
        }

        return context;
    }

    private async Task<ActiveTenantContext> RequireManagePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("review-tasks.manage", cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskPermissions.Manage, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, "review-tasks.manage", "missing_permission", "Review task manage permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Review task manage permission is required.");
        }

        return context;
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_mismatch", "The requested record belongs to a different tenant.", cancellationToken);
        throw new TenantAccessDeniedException("The requested record belongs to a different tenant.");
    }
}
