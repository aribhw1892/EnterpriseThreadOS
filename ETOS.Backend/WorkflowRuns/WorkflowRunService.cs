using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Workflows;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.WorkflowRuns;

public interface IWorkflowRunService
{
    Task<IReadOnlyCollection<WorkflowRunSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<WorkflowRunDetailResponse> GetAsync(Guid workflowRunId, CancellationToken cancellationToken);
}

public sealed class WorkflowRunService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IWorkflowRunService
{
    public async Task<IReadOnlyCollection<WorkflowRunSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("workflow-runs.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("workflow-runs.list", cancellationToken);

        return await dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId)
            .OrderByDescending(item => item.StartedAt)
            .Take(100)
            .Select(item => new WorkflowRunSummaryResponse(
                item.Id,
                item.WorkflowVersionId,
                item.Status,
                item.IsPreview,
                item.SafeModeApplied,
                item.InputSafeSummaryJson,
                item.RequestedByUserId,
                item.AiTraceRecordId,
                item.StartedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowRunDetailResponse> GetAsync(Guid workflowRunId, CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("workflow-runs.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("workflow-runs.get", cancellationToken);

        var run = await dbContext.WorkflowRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == workflowRunId, cancellationToken)
            ?? throw new RequestValidationException("Workflow run was not found.");

        if (run.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "workflow-runs.get",
                "tenant_access_denied",
                "Record belongs to a different tenant.",
                cancellationToken);
            throw new TenantAccessDeniedException("Record is not available in the active tenant.");
        }

        var safeModeEvents = await dbContext.SafeModeEvents
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.WorkflowRunId == workflowRunId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new SafeModeEventSummaryResponse(
                item.Id,
                item.StepKey,
                item.EventKind,
                item.Reason,
                item.PolicyRuleKey,
                item.BlockedAction,
                item.AgentRunId,
                item.ToolRunId,
                item.ReviewTaskArtifactId,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        var childAgentRunIds = await dbContext.AgentRuns
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.ParentWorkflowRunId == workflowRunId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var childToolRunIds = await dbContext.ToolRuns
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.ParentWorkflowRunId == workflowRunId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        return new WorkflowRunDetailResponse(
            run.Id,
            run.TenantId,
            run.WorkflowVersionId,
            run.Status,
            run.IsPreview,
            run.SafeModeApplied,
            run.PartialCompletion,
            run.InputSafeSummaryJson,
            run.OutputSafeSummaryJson,
            run.StepResultsJson,
            run.InheritedRiskSnapshotJson,
            run.RuntimeTrustRecalculationJson,
            run.RecommendationArtifactIdsJson,
            run.ReviewTaskArtifactIdsJson,
            run.AuditRecordId,
            run.AiTraceRecordId,
            run.RequestedByUserId,
            run.StartedAt,
            run.CompletedAt,
            safeModeEvents,
            childAgentRunIds,
            childToolRunIds);
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowRunPermissions.Read, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Execute, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Preview, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {WorkflowRunPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks workflow run read permission.");
    }
}
