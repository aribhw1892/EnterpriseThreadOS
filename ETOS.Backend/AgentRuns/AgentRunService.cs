using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AgentRuns;

public interface IAgentRunService
{
    Task<IReadOnlyCollection<AgentRunSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AgentRunDetailResponse> GetAsync(Guid agentRunId, CancellationToken cancellationToken);
}

public sealed class AgentRunService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IAgentRunService
{
    public async Task<IReadOnlyCollection<AgentRunSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("agent-runs.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agent-runs.list", cancellationToken);

        return await dbContext.AgentRuns
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId)
            .OrderByDescending(item => item.StartedAt)
            .Take(100)
            .Select(item => new AgentRunSummaryResponse(
                item.Id,
                item.AgentVersionId,
                item.ParentWorkflowRunId,
                item.Status,
                item.IsPreview,
                item.IsDryRun,
                item.InputSafeSummaryJson,
                item.RequestedByUserId,
                item.AiTraceRecordId,
                item.StartedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentRunDetailResponse> GetAsync(Guid agentRunId, CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("agent-runs.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agent-runs.get", cancellationToken);

        var run = await dbContext.AgentRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == agentRunId, cancellationToken)
            ?? throw new RequestValidationException("Agent run was not found.");

        if (run.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "agent-runs.get",
                "tenant_access_denied",
                "Record belongs to a different tenant.",
                cancellationToken);
            throw new TenantAccessDeniedException("Record is not available in the active tenant.");
        }

        return new AgentRunDetailResponse(
            run.Id,
            run.TenantId,
            run.AgentVersionId,
            run.ParentWorkflowRunId,
            run.Status,
            run.IsPreview,
            run.IsDryRun,
            run.SafeModeApplied,
            run.InputSafeSummaryJson,
            run.OutputSafeSummaryJson,
            run.StructuredOutputJson,
            run.DerivedRiskSnapshotJson,
            run.FallbackUsedJson,
            run.ValidationResultJson,
            run.ErrorSafeSummary,
            run.GovernedContextSummaryJson,
            run.RetrievalRunId,
            run.RecommendationArtifactId,
            run.AuditRecordId,
            run.AiTraceRecordId,
            run.RequestedByUserId,
            run.StartedAt,
            run.CompletedAt);
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentRunPermissions.Read, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Execute, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Test, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {AgentRunPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent run read permission.");
    }
}
