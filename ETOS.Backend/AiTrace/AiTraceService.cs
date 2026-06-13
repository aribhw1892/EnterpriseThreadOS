using System.Text.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AiTrace;

public interface IAiTraceService
{
    Task<IReadOnlyCollection<AiTraceSummaryResponse>> ListTracesAsync(CancellationToken cancellationToken);
    Task<AiTraceDetailResponse> GetTraceAsync(Guid traceId, CancellationToken cancellationToken);
    Task<AiTraceDetailResponse?> GetTraceByRetrievalRunAsync(Guid retrievalRunId, CancellationToken cancellationToken);
    Task<AiTraceExportFileResult> ExportTraceAsync(Guid traceId, CancellationToken cancellationToken);
}

public sealed class AiTraceService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder) : IAiTraceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<AiTraceSummaryResponse>> ListTracesAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("ai_trace.traces.list", AiTracePermissions.Read, cancellationToken);
        return await dbContext.AiTraceRecords
            .AsNoTracking()
            .Where(trace => trace.TenantId == context.TenantId)
            .OrderByDescending(trace => trace.CreatedAt)
            .Select(trace => new AiTraceSummaryResponse(
                trace.Id,
                trace.TenantId,
                trace.TraceKind,
                trace.IntentKey,
                trace.StrategyKey,
                trace.Status,
                trace.SafeSummary,
                trace.RequestedByUserId,
                trace.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AiTraceDetailResponse> GetTraceAsync(Guid traceId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("ai_trace.traces.get", AiTracePermissions.Read, cancellationToken);
        return await GetTraceInternalAsync(traceId, context, "ai_trace.traces.get", cancellationToken);
    }

    public async Task<AiTraceDetailResponse?> GetTraceByRetrievalRunAsync(Guid retrievalRunId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("ai_trace.traces.get_by_run", AiTracePermissions.Read, cancellationToken);
        var traceId = await dbContext.AiTraceRecords
            .AsNoTracking()
            .Where(trace => trace.TenantId == context.TenantId && trace.RetrievalRunId == retrievalRunId)
            .OrderByDescending(trace => trace.CreatedAt)
            .Select(trace => trace.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (traceId == Guid.Empty)
        {
            return null;
        }

        return await GetTraceInternalAsync(traceId, context, "ai_trace.traces.get_by_run", cancellationToken);
    }

    public async Task<AiTraceExportFileResult> ExportTraceAsync(Guid traceId, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("ai_trace.traces.export", cancellationToken);
        var hasExportPermission = await HasExportPermissionAsync(context, cancellationToken);
        if (!hasExportPermission)
        {
            await RecordExportDeniedAsync(context, traceId, cancellationToken);
            throw new TenantAccessDeniedException("User lacks AI Trace export permission.");
        }

        var includeSensitive = await HasAdminPermissionAsync(context, cancellationToken);
        var trace = await GetTraceInternalAsync(traceId, context, "ai_trace.traces.export", cancellationToken);
        var policyVersion = await ResolvePolicyVersionAsync(trace.ConfidenceImpact.PolicyKey, context.TenantId, cancellationToken);
        var export = AiTraceExportBuilder.BuildExport(trace, context.UserId, includeSensitive, policyVersion);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "ai_trace.export",
                AuditResult.Export,
                null,
                $"AI Trace '{trace.Id}' exported with evidence level '{export.Metadata.EvidenceLevel}'.",
                nameof(AiTraceRecord),
                trace.Id.ToString(),
                trace.ConfidenceImpact.PolicyKey,
                policyVersion,
                RetentionCategory: AuditRetentionCategory.Export,
                IsArchiveEligible: true),
            cancellationToken);

        var exportRecord = new AiTraceExportRecord
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            AiTraceRecordId = trace.Id,
            ExportedByUserId = context.UserId,
            ExportHash = export.Metadata.ExportHash,
            RedactionMetadataJson = AiTraceExportBuilder.SerializeRedactionMetadata(export.Metadata.RedactionMetadata),
            EvidenceLevel = export.Metadata.EvidenceLevel,
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.AiTraceExportRecords.Add(exportRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        return export with
        {
            Metadata = export.Metadata with { ExportRecordId = exportRecord.Id }
        };
    }

    private async Task<AiTraceDetailResponse> GetTraceInternalAsync(
        Guid traceId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var includeSensitive = await HasAdminPermissionAsync(context, cancellationToken);
        var trace = await dbContext.AiTraceRecords
            .AsNoTracking()
            .Include(item => item.ArtifactLinks)
            .SingleOrDefaultAsync(item => item.Id == traceId, cancellationToken)
            ?? throw new RequestValidationException("AI Trace was not found.");

        if (trace.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, "AI Trace belongs to a different tenant.", cancellationToken);
        }

        return ToDetailResponse(trace, includeSensitive);
    }

    private async Task RecordExportDeniedAsync(ActiveTenantContext context, Guid traceId, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "ai_trace.traces.export",
            "export_denied",
            $"The user lacks the {AiTracePermissions.Export} permission.",
            cancellationToken);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "ai_trace.traces.export",
                AuditResult.Denied,
                "export_denied",
                $"Denied AI Trace export for trace '{traceId}'.",
                nameof(AiTraceRecord),
                traceId.ToString(),
                RetentionCategory: AuditRetentionCategory.Security,
                IsArchiveEligible: true),
            cancellationToken);

        await auditRecorder.RecordSecurityEventAsync(
            new SecurityEventWriteRequest(
                context.TenantId,
                context.UserId,
                SecurityEventType.ExportDenied,
                SecurityEventSeverity.Medium,
                "ai_trace.traces.export",
                "export_denied",
                $"Denied AI Trace export for trace '{traceId}'.",
                audit.Id,
                ReviewTaskReady: true,
                ReviewTaskHint: "Review repeated AI Trace export denials."),
            cancellationToken);
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, string safeSummary, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("AI Trace is not available in the active tenant.");
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await HasReadPermissionAsync(context, permissionKey, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks AI Trace permission.");
        }

        return context;
    }

    private async Task<bool> HasReadPermissionAsync(ActiveTenantContext context, string permissionKey, CancellationToken cancellationToken)
    {
        return await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
    }

    private async Task<bool> HasExportPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        return await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Export, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
    }

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        return await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
    }

    private async Task<string?> ResolvePolicyVersionAsync(string? policyKey, Guid tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return null;
        }

        return await dbContext.PolicyVersions
            .AsNoTracking()
            .Where(policy => policy.TenantId == tenantId && policy.PolicyKey == policyKey)
            .OrderByDescending(policy => policy.CreatedAt)
            .Select(policy => policy.VersionLabel)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static AiTraceDetailResponse ToDetailResponse(AiTraceRecord trace, bool includeSensitive)
    {
        return new AiTraceDetailResponse(
            trace.Id,
            trace.TenantId,
            trace.RetrievalRunId,
            trace.ContextPackageId,
            trace.AuditRecordId,
            trace.TraceKind,
            trace.IntentKey,
            trace.StrategyKey,
            trace.QueryText,
            trace.Status,
            trace.SafeSummary,
            Deserialize<IReadOnlyCollection<AiTraceSourceSummaryResponse>>(trace.SourcesSummaryJson),
            Deserialize<IReadOnlyCollection<TraceContextSummaryResponse>>(trace.FilteredSummariesJson),
            Deserialize<IReadOnlyCollection<TraceDeniedSummaryResponse>>(trace.DeniedSafeSummariesJson),
            includeSensitive
                ? Deserialize<IReadOnlyCollection<TraceSensitiveDeniedReferenceResponse>>(trace.SensitiveDeniedReferencesJson)
                : null,
            Deserialize<AiTraceConfidenceImpactResponse>(trace.ConfidenceImpactJson),
            trace.PromptTemplateVersionLabel,
            trace.OutputSchemaVersionLabel,
            trace.GeneratedOutputJson,
            trace.ArtifactLinks
                .OrderBy(link => link.LinkKind)
                .Select(link => new AiTraceArtifactLinkResponse(link.Id, link.LinkKind, link.ObjectType, link.ObjectId))
                .ToList(),
            trace.RequestedByUserId,
            trace.CreatedAt);
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored AI Trace JSON could not be deserialized.");
}
