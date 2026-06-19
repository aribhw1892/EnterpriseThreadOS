using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public interface IToolRunService
{
    Task<IReadOnlyCollection<ToolRunSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ToolRunDetailResponse> GetAsync(Guid toolRunId, CancellationToken cancellationToken);
}

public sealed class ToolRunService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IToolRunService
{
    public async Task<IReadOnlyCollection<ToolRunSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("tool-runs.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("tool-runs.list", cancellationToken);

        return await dbContext.ToolRuns
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new ToolRunSummaryResponse(
                item.Id,
                item.ToolDefinitionVersionId,
                item.Status,
                item.IsDryRun,
                item.InputSafeSummaryJson,
                item.RequestedByUserId,
                item.AiTraceRecordId,
                item.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ToolRunDetailResponse> GetAsync(Guid toolRunId, CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("tool-runs.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("tool-runs.get", cancellationToken);

        var run = await dbContext.ToolRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == toolRunId, cancellationToken)
            ?? throw new RequestValidationException("Tool run was not found.");

        if (run.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "tool-runs.get",
                "tenant_access_denied",
                "Record belongs to a different tenant.",
                cancellationToken);
            throw new TenantAccessDeniedException("Record is not available in the active tenant.");
        }

        return new ToolRunDetailResponse(
            run.Id,
            run.TenantId,
            run.ToolDefinitionVersionId,
            run.ConnectorDefinitionVersionId,
            run.ParentAgentRunId,
            run.Status,
            run.IsDryRun,
            run.InputSafeSummaryJson,
            run.OutputSafeSummaryJson,
            run.ValidationResultJson,
            run.CompatibilityNotesJson,
            run.ErrorSafeSummary,
            run.ConnectorCredentialSafeSummaryJson,
            run.RetrievalRunId,
            run.AuditRecordId,
            run.AiTraceRecordId,
            run.RequestedByUserId,
            run.CreatedAt,
            run.CompletedAt);
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolRunPermissions.Read, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {ToolRunPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks tool run read permission.");
    }
}
