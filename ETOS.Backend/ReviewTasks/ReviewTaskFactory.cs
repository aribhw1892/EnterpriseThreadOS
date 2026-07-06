using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.WorkflowRuns;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ReviewTasks;

public interface IReviewTaskFactory
{
    Task<CreateReviewTaskResponse> CreateManualAsync(CreateReviewTaskRequest request, CancellationToken cancellationToken);
    Task<CreateReviewTaskResponse> FromRecommendationActionAsync(
        Guid artifactId,
        Guid versionId,
        Guid actionId,
        CancellationToken cancellationToken);
    Task<CreateReviewTaskResponse> FromDataQualityIssueAsync(Guid issueId, CancellationToken cancellationToken);
    Task<CreateReviewTaskResponse> FromSecurityEventAsync(Guid eventId, CancellationToken cancellationToken);
    Task<CreateReviewTaskResponse> FromAccessRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<CreateReviewTaskResponse> FromWorkflowOutputAsync(
        Guid workflowRunId,
        string stepKey,
        string? title,
        CancellationToken cancellationToken);
    Task<CreateReviewTaskResponse> FromSafeModeEventAsync(Guid safeModeEventId, CancellationToken cancellationToken);
}

public sealed class ReviewTaskFactory(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IReviewTaskTemplateResolver templateResolver,
    IReviewTaskPriorityDeriver priorityDeriver,
    IReviewTaskChainService chainService) : IReviewTaskFactory
{
    public async Task<CreateReviewTaskResponse> CreateManualAsync(
        CreateReviewTaskRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        await ReviewTaskMembershipValidator.ValidateAssigneesAsync(dbContext, context.TenantId, request.PrimaryOwnerUserId, request.Participants, cancellationToken);

        var template = await templateResolver.ResolvePublishedTemplateAsync(
            context.TenantId,
            request.SourceType,
            null,
            null,
            cancellationToken);

        var severity = request.Severity ?? RecommendationRiskState.Medium;
        var trustState = request.TrustState ?? TrustState.Provisional;
        var conflictState = request.ConflictState ?? RecommendationConflictState.None;
        var priority = priorityDeriver.Derive(severity, trustState, conflictState, template?.Template);

        return await PersistTaskAsync(
            context,
            request.Title,
            request.ReviewTaskType,
            request.SourceType,
            request.SourceReference ?? Guid.NewGuid().ToString(),
            request.PrimaryOwnerUserId,
            request.AssignedRoleKey,
            request.Participants,
            priority,
            severity,
            trustState,
            conflictState,
            null,
            request.EvidenceReferences?.Select(item => new ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument
            {
                LinkId = Guid.NewGuid(),
                EvidenceType = item.EvidenceType,
                SourceId = item.SourceId,
                SafeSummary = item.SafeSummary.Trim(),
                TrustState = item.TrustState ?? TrustState.Provisional
            }).ToList(),
            template?.VersionId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            request.DueDate,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            ReviewTaskStatus.Open,
            cancellationToken);
    }

    public async Task<CreateReviewTaskResponse> FromRecommendationActionAsync(
        Guid artifactId,
        Guid versionId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var recommendationArtifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Recommendation artifact was not found.");

        if (recommendationArtifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "review-tasks.from-recommendation", cancellationToken);
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Recommendation version was not found.");

        var recommendation = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var action = recommendation.SuggestedActions.SingleOrDefault(item => item.ActionId == actionId)
            ?? throw new RequestValidationException("Suggested action was not found.");

        if (action.Status == SuggestedActionStatus.ConvertedToReviewTask)
        {
            throw new RequestValidationException("Suggested action was already converted to a review task.");
        }

        var template = await templateResolver.ResolvePublishedTemplateAsync(
            context.TenantId,
            ReviewTaskSourceType.Recommendation,
            action.RequiredReviewPath,
            recommendation.RecommendationType,
            cancellationToken);

        var priority = priorityDeriver.Derive(
            action.RiskScore,
            recommendation.TrustState,
            recommendation.ConflictState,
            template?.Template);

        var evidence = recommendation.EvidenceLinks.Select(link => new ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument
        {
            LinkId = Guid.NewGuid(),
            EvidenceType = link.EvidenceType,
            SourceId = link.SourceId,
            SafeSummary = link.SafeSummary,
            TrustState = link.TrustState
        }).ToList();

        var dqIssueId = evidence.FirstOrDefault(link => link.EvidenceType == EvidenceLinkType.DataQualityIssue)?.SourceId;
        var initialStatus = ReviewTaskStatus.Open;
        string? blockingReason = null;
        Guid? prerequisiteTaskId = null;

        if (template?.Template.RequiresDataQualityPrerequisite == true && dqIssueId is Guid linkedIssueId)
        {
            var dqTask = await FromDataQualityIssueInternalAsync(context, linkedIssueId, cancellationToken);
            prerequisiteTaskId = dqTask.ArtifactId;
            initialStatus = ReviewTaskStatus.Blocked;
            blockingReason = $"Waiting for data quality review task '{dqTask.ArtifactId}'.";
        }

        var created = await PersistTaskAsync(
            context,
            action.Title,
            template?.Template.ReviewTaskType ?? "business-action-review",
            ReviewTaskSourceType.Recommendation,
            $"{artifactId}:{actionId}",
            context.UserId,
            null,
            null,
            priority,
            action.RiskScore,
            recommendation.TrustState,
            recommendation.ConflictState,
            null,
            evidence,
            template?.VersionId,
            artifactId,
            versionId,
            actionId,
            dqIssueId,
            null,
            null,
            recommendation.Explainability?.AiTraceId,
            recommendation.Explainability?.ContextPackageId,
            null,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            initialStatus,
            cancellationToken,
            blockingReason,
            prerequisiteTaskId is Guid prereq ? [prereq] : null);

        if (prerequisiteTaskId is Guid blockingTaskId)
        {
            await chainService.CreateChainLinkAsync(
                context.TenantId,
                context.UserId,
                created.ArtifactId,
                blockingTaskId,
                ReviewTaskChainReason.DataQualityPrerequisite,
                ReviewTaskBlockingCondition.PrerequisiteAccepted,
                cancellationToken);
        }

        recommendation.SuggestedActions = recommendation.SuggestedActions
            .Select(item => item.ActionId == actionId ? item with { Status = SuggestedActionStatus.ConvertedToReviewTask } : item)
            .ToList();
        version.PayloadJson = RecommendationPayloadParser.Serialize(recommendation);
        recommendationArtifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "review-tasks.from-recommendation",
                AuditResult.Success,
                null,
                $"Review task created from recommendation action '{actionId}'.",
                nameof(Artifact),
                created.ArtifactId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return created;
    }

    public async Task<CreateReviewTaskResponse> FromDataQualityIssueAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        return await FromDataQualityIssueInternalAsync(context, issueId, cancellationToken);
    }

    public async Task<CreateReviewTaskResponse> FromSecurityEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var securityEvent = await dbContext.SecurityEvents
            .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new RequestValidationException("Security event was not found.");

        if (securityEvent.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "review-tasks.from-security-event", cancellationToken);
        }

        var template = await templateResolver.ResolvePublishedTemplateAsync(
            context.TenantId,
            ReviewTaskSourceType.SecurityEvent,
            null,
            RecommendationType.Security,
            cancellationToken);

        var severity = MapSecuritySeverity(securityEvent.Severity);
        var priority = priorityDeriver.Derive(severity, TrustState.Provisional, RecommendationConflictState.None, template?.Template);

        var created = await PersistTaskAsync(
            context,
            $"Review security event {securityEvent.EventType}",
            template?.Template.ReviewTaskType ?? "governance-security-review",
            ReviewTaskSourceType.SecurityEvent,
            eventId.ToString(),
            context.UserId,
            null,
            null,
            priority,
            severity,
            TrustState.Provisional,
            RecommendationConflictState.None,
            null,
            [
                new ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument
                {
                    LinkId = Guid.NewGuid(),
                    EvidenceType = EvidenceLinkType.ManualNote,
                    SourceId = eventId,
                    SafeSummary = securityEvent.SafeSummary,
                    TrustState = TrustState.Provisional
                }
            ],
            template?.VersionId,
            null,
            null,
            null,
            null,
            eventId,
            null,
            null,
            null,
            null,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            ReviewTaskStatus.Open,
            cancellationToken);

        securityEvent.ReviewTaskCreatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<CreateReviewTaskResponse> FromAccessRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var accessRequest = await dbContext.AccessRequests
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new RequestValidationException("Access request was not found.");

        if (accessRequest.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "review-tasks.from-access-request", cancellationToken);
        }

        var template = await templateResolver.ResolvePublishedTemplateAsync(
            context.TenantId,
            ReviewTaskSourceType.AccessRequest,
            "ACCESS_REQUEST",
            null,
            cancellationToken);

        var priority = priorityDeriver.Derive(
            RecommendationRiskState.Medium,
            TrustState.Provisional,
            RecommendationConflictState.None,
            template?.Template);

        return await PersistTaskAsync(
            context,
            $"Review access request for {accessRequest.PermissionKey}",
            template?.Template.ReviewTaskType ?? "access-request-review",
            ReviewTaskSourceType.AccessRequest,
            requestId.ToString(),
            context.UserId,
            null,
            null,
            priority,
            RecommendationRiskState.Medium,
            TrustState.Provisional,
            RecommendationConflictState.None,
            null,
            [
                new ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument
                {
                    LinkId = Guid.NewGuid(),
                    EvidenceType = EvidenceLinkType.ManualNote,
                    SourceId = requestId,
                    SafeSummary = accessRequest.Reason,
                    TrustState = TrustState.Provisional
                }
            ],
            template?.VersionId,
            null,
            null,
            null,
            null,
            null,
            requestId,
            null,
            null,
            null,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            ReviewTaskStatus.Open,
            cancellationToken);
    }

    public async Task<CreateReviewTaskResponse> FromWorkflowOutputAsync(
        Guid workflowRunId,
        string stepKey,
        string? title,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var workflowRun = await dbContext.WorkflowRuns
            .SingleOrDefaultAsync(item => item.Id == workflowRunId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Workflow run was not found.");

        var templateVersionId = await ResolveWorkflowReviewTaskTemplateVersionIdAsync(
            workflowRun.WorkflowVersionId,
            stepKey,
            cancellationToken);

        var template = templateVersionId is Guid versionId
            ? await templateResolver.ResolvePublishedTemplateAsync(
                context.TenantId,
                ReviewTaskSourceType.Workflow,
                stepKey,
                null,
                cancellationToken)
            : null;

        var taskTitle = string.IsNullOrWhiteSpace(title)
            ? $"Review workflow output: {stepKey}"
            : title.Trim();

        var priority = priorityDeriver.Derive(
            RecommendationRiskState.Medium,
            TrustState.Provisional,
            RecommendationConflictState.None,
            template?.Template);

        var evidence = new List<ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument>
        {
            new()
            {
                LinkId = Guid.NewGuid(),
                EvidenceType = EvidenceLinkType.WorkflowRun,
                SourceId = workflowRun.Id,
                SafeSummary = workflowRun.OutputSafeSummaryJson ?? $"Workflow run {workflowRun.Status}.",
                TrustState = TrustState.Provisional
            }
        };

        return await PersistTaskAsync(
            context,
            taskTitle,
            template?.Template.ReviewTaskType ?? "workflow-output-review",
            ReviewTaskSourceType.Workflow,
            $"{workflowRunId}:{stepKey}",
            context.UserId,
            null,
            null,
            priority,
            RecommendationRiskState.Medium,
            TrustState.Provisional,
            RecommendationConflictState.None,
            null,
            evidence,
            templateVersionId ?? template?.VersionId,
            null,
            null,
            null,
            null,
            null,
            null,
            workflowRun.AiTraceRecordId,
            null,
            null,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            ReviewTaskStatus.Open,
            cancellationToken);
    }

    public async Task<CreateReviewTaskResponse> FromSafeModeEventAsync(
        Guid safeModeEventId,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var safeModeEvent = await dbContext.SafeModeEvents
            .SingleOrDefaultAsync(item => item.Id == safeModeEventId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Safe mode event was not found.");

        var workflowRun = await dbContext.WorkflowRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == safeModeEvent.WorkflowRunId, cancellationToken)
            ?? throw new RequestValidationException("Workflow run was not found.");

        var template = await templateResolver.ResolvePublishedTemplateAsync(
            context.TenantId,
            ReviewTaskSourceType.Workflow,
            "safe-mode-warning",
            RecommendationType.Policy,
            cancellationToken);

        var priority = priorityDeriver.Derive(
            RecommendationRiskState.High,
            TrustState.Provisional,
            RecommendationConflictState.None,
            template?.Template);

        var created = await PersistTaskAsync(
            context,
            $"Review safe mode skip: {safeModeEvent.StepKey}",
            template?.Template.ReviewTaskType ?? "workflow-safe-mode-review",
            ReviewTaskSourceType.Workflow,
            safeModeEvent.Id.ToString(),
            context.UserId,
            null,
            null,
            priority,
            RecommendationRiskState.High,
            TrustState.Provisional,
            RecommendationConflictState.None,
            null,
            [
                new ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument
                {
                    LinkId = Guid.NewGuid(),
                    EvidenceType = EvidenceLinkType.WorkflowRun,
                    SourceId = workflowRun.Id,
                    SafeSummary = safeModeEvent.Reason,
                    TrustState = TrustState.Provisional
                }
            ],
            template?.VersionId,
            null,
            null,
            null,
            null,
            null,
            null,
            workflowRun.AiTraceRecordId,
            null,
            null,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            ReviewTaskStatus.Open,
            cancellationToken);

        safeModeEvent.ReviewTaskArtifactId = created.ArtifactId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<Guid?> ResolveWorkflowReviewTaskTemplateVersionIdAsync(
        Guid workflowVersionId,
        string stepKey,
        CancellationToken cancellationToken)
    {
        var payloadJson = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(item => item.Id == workflowVersionId)
            .Select(item => item.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        var payload = Workflows.WorkflowDefinitionPayloadParser.Deserialize(payloadJson);
        var step = Workflows.WorkflowDefinitionPayloadParser
            .DeserializeWorkflowDefinitionJson(payload.WorkflowDefinitionJson ?? "[]")
            .FirstOrDefault(item => item.StepKey?.Equals(stepKey, StringComparison.OrdinalIgnoreCase) == true);

        return step?.ReviewTaskTemplateVersionId;
    }

    private async Task<CreateReviewTaskResponse> FromDataQualityIssueInternalAsync(
        ActiveTenantContext context,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.DataQualityIssues
            .SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken)
            ?? throw new RequestValidationException("Data quality issue was not found.");

        if (issue.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "review-tasks.from-data-quality-issue", cancellationToken);
        }

        var template = await templateResolver.ResolvePublishedTemplateAsync(
            context.TenantId,
            ReviewTaskSourceType.DataQuality,
            null,
            RecommendationType.DataQuality,
            cancellationToken);

        var severity = MapDataQualitySeverity(issue.Severity);
        var priority = priorityDeriver.Derive(severity, issue.ResultingTrustState, RecommendationConflictState.None, template?.Template);

        var created = await PersistTaskAsync(
            context,
            issue.Title,
            template?.Template.ReviewTaskType ?? "data-quality-review",
            ReviewTaskSourceType.DataQuality,
            issueId.ToString(),
            context.UserId,
            null,
            null,
            priority,
            severity,
            issue.ResultingTrustState,
            RecommendationConflictState.None,
            null,
            [
                new ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument
                {
                    LinkId = Guid.NewGuid(),
                    EvidenceType = EvidenceLinkType.DataQualityIssue,
                    SourceId = issueId,
                    SafeSummary = issue.EvidenceSummary,
                    TrustState = issue.ResultingTrustState
                }
            ],
            template?.VersionId,
            null,
            null,
            null,
            issueId,
            issue.SecurityEventId,
            null,
            null,
            null,
            null,
            template?.Template.EscalationPath is { Enabled: true } escalation
                ? new ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument
                {
                    Enabled = true,
                    EscalationTargetRoleKey = escalation.EscalationTargetRoleKey,
                    EscalationPolicyId = escalation.EscalationPolicyId,
                    SlaPolicyVersion = escalation.SlaPolicyVersion
                }
                : null,
            ReviewTaskStatus.Open,
            cancellationToken);

        issue.ReviewHookCreatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<CreateReviewTaskResponse> PersistTaskAsync(
        ActiveTenantContext context,
        string title,
        string reviewTaskType,
        ReviewTaskSourceType sourceType,
        string sourceReference,
        Guid? primaryOwnerUserId,
        string? assignedRoleKey,
        IReadOnlyCollection<CreateReviewTaskParticipantRequest>? participants,
        ReviewTaskPriority priority,
        RecommendationRiskState severity,
        TrustState trustState,
        RecommendationConflictState conflictState,
        decimal? confidenceScore,
        IReadOnlyCollection<ReviewTaskPayloadParser.ReviewTaskEvidenceReferenceDocument>? evidenceReferences,
        Guid? reviewTemplateVersionId,
        Guid? recommendationArtifactId,
        Guid? recommendationVersionId,
        Guid? suggestedActionId,
        Guid? dataQualityIssueId,
        Guid? securityEventId,
        Guid? accessRequestId,
        Guid? aiTraceId,
        Guid? contextPackageId,
        DateTimeOffset? dueDate,
        ReviewTaskPayloadParser.ReviewTaskEscalationPlaceholderDocument? escalationPlaceholder,
        ReviewTaskStatus status,
        CancellationToken cancellationToken,
        string? blockingReason = null,
        IReadOnlyCollection<Guid>? prerequisiteTaskIds = null)
    {
        var participantDocuments = participants?.Select(item => new ReviewTaskPayloadParser.ReviewTaskParticipantDocument
        {
            UserId = item.UserId,
            Role = item.Role
        }).ToList();

        var payload = ReviewTaskPayloadParser.CreateDefault(
            title,
            sourceType,
            sourceReference,
            reviewTaskType,
            primaryOwnerUserId,
            assignedRoleKey,
            participantDocuments,
            priority,
            severity,
            trustState,
            conflictState,
            confidenceScore,
            evidenceReferences,
            reviewTemplateVersionId,
            recommendationArtifactId,
            recommendationVersionId,
            suggestedActionId,
            dataQualityIssueId,
            securityEventId,
            accessRequestId,
            aiTraceId,
            contextPackageId,
            dueDate,
            escalationPlaceholder,
            status);

        payload.BlockingReason = blockingReason;
        payload.PrerequisiteTaskIds = prerequisiteTaskIds?.ToList() ?? [];

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskArtifactTypes.ReviewTask,
            NormalizedArtifactType = ReviewTaskArtifactTypes.ReviewTask.ToUpperInvariant(),
            Name = title.Trim(),
            Description = $"Review task from {sourceType}.",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = title.Trim(),
            PayloadJson = ReviewTaskPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateReviewTaskResponse(artifact.Id, version.Id, version.VersionLabel, status);
    }

    private static RecommendationRiskState MapDataQualitySeverity(DataQualitySeverity severity)
        => severity switch
        {
            DataQualitySeverity.Critical => RecommendationRiskState.Critical,
            DataQualitySeverity.High => RecommendationRiskState.High,
            DataQualitySeverity.Medium => RecommendationRiskState.Medium,
            _ => RecommendationRiskState.Low
        };

    private static RecommendationRiskState MapSecuritySeverity(SecurityEventSeverity severity)
        => severity switch
        {
            SecurityEventSeverity.Critical => RecommendationRiskState.Critical,
            SecurityEventSeverity.High => RecommendationRiskState.High,
            SecurityEventSeverity.Medium => RecommendationRiskState.Medium,
            _ => RecommendationRiskState.Low
        };

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("review-tasks.create", cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ReviewTaskPermissions.Create, cancellationToken))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, "review-tasks.create", "missing_permission", "Review task create permission is required.", cancellationToken);
            throw new TenantAccessDeniedException("Review task create permission is required.");
        }

        return context;
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_mismatch", "The requested record belongs to a different tenant.", cancellationToken);
        throw new TenantAccessDeniedException("The requested record belongs to a different tenant.");
    }
}

internal static class ReviewTaskMembershipValidator
{
    public static async Task ValidateAssigneesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid? primaryOwnerUserId,
        IReadOnlyCollection<CreateReviewTaskParticipantRequest>? participants,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>();
        if (primaryOwnerUserId is Guid ownerId)
        {
            userIds.Add(ownerId);
        }

        if (participants is not null)
        {
            foreach (var participant in participants)
            {
                userIds.Add(participant.UserId);
            }
        }

        foreach (var userId in userIds)
        {
            var isMember = await dbContext.TenantMemberships.AnyAsync(
                membership => membership.TenantId == tenantId
                    && membership.UserId == userId
                    && membership.IsActive,
                cancellationToken);

            if (!isMember)
            {
                throw new TenantAccessDeniedException($"User '{userId}' is not an active tenant member and cannot be assigned.");
            }
        }
    }
}
