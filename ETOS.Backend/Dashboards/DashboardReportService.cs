using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Dashboards;

public interface IDashboardReportService
{
    Task<IReadOnlyCollection<GovernanceKpiPlaceholderResponse>> ListKpiPlaceholdersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DashboardReportArtifactSummaryResponse>> ListDashboardArtifactsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DashboardReportArtifactSummaryResponse>> ListReportArtifactsAsync(CancellationToken cancellationToken);
    Task<DashboardReportTemplateResponse> GetTemplateAsync(string artifactType, Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<DashboardReportPreviewResponse> PreviewAsync(string artifactType, Guid artifactId, Guid versionId, DashboardReportPreviewRequest request, CancellationToken cancellationToken);
    Task<MarkDashboardReportReadyResponse> MarkReadyAsync(string artifactType, Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<DashboardReportExportFileResult> ExportAsync(string artifactType, Guid artifactId, Guid versionId, DashboardReportPreviewRequest request, CancellationToken cancellationToken);
}

public sealed class DashboardReportService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IGovernedQueryService governedQueryService,
    IClassificationPolicyService classificationPolicyService) : IDashboardReportService
{
    public Task<IReadOnlyCollection<GovernanceKpiPlaceholderResponse>> ListKpiPlaceholdersAsync(CancellationToken cancellationToken)
        => Task.FromResult(PlatformGovernanceKpiPlaceholders.Catalog);

    public async Task<IReadOnlyCollection<DashboardReportArtifactSummaryResponse>> ListDashboardArtifactsAsync(CancellationToken cancellationToken)
        => await ListArtifactsByTypeAsync(DashboardReportArtifactTypes.Dashboard, cancellationToken);

    public async Task<IReadOnlyCollection<DashboardReportArtifactSummaryResponse>> ListReportArtifactsAsync(CancellationToken cancellationToken)
        => await ListArtifactsByTypeAsync(DashboardReportArtifactTypes.Report, cancellationToken);

    public async Task<DashboardReportTemplateResponse> GetTemplateAsync(
        string artifactType,
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactType, artifactId, versionId, "dashboards_reports.template.get", cancellationToken);
        return DashboardReportTemplateParser.Parse(artifactType, artifactId, versionId, version.VersionLabel, version.PayloadJson ?? "{}");
    }

    public async Task<DashboardReportPreviewResponse> PreviewAsync(
        string artifactType,
        Guid artifactId,
        Guid versionId,
        DashboardReportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        await RequirePreviewPermissionAsync("dashboards_reports.preview.run", cancellationToken);
        var template = await GetTemplateAsync(artifactType, artifactId, versionId, cancellationToken);
        return await BuildPreviewAsync(template, request, cancellationToken);
    }

    public async Task<MarkDashboardReportReadyResponse> MarkReadyAsync(
        string artifactType,
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("dashboards_reports.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactType, artifactId, versionId, "dashboards_reports.readiness.mark", cancellationToken);

        if (artifact.OwnerUserId != context.UserId
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Admin, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "dashboards_reports.readiness.mark",
                "permission_denied",
                "Only an artifact owner or artifact administrator may mark dashboard/report versions ready.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks artifact ownership or administration permission.");
        }

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var template = DashboardReportTemplateParser.Parse(artifactType, artifactId, versionId, version.VersionLabel, version.PayloadJson ?? "{}");
        var validationNotes = DashboardReportReadinessValidator.ValidateForReady(template);
        if (validationNotes.Count > 0)
        {
            throw new RequestValidationException(string.Join(" ", validationNotes));
        }

        await classificationPolicyService.EvaluateArtifactPolicyRiskAsync(context.TenantId, version.Id, cancellationToken);
        await dbContext.Entry(version).ReloadAsync(cancellationToken);

        version.ReadinessState = version.PolicyRiskStatus switch
        {
            ArtifactPolicyRiskStatus.RequiresApproval => ArtifactReadinessState.RequiresApproval,
            ArtifactPolicyRiskStatus.Blocked => ArtifactReadinessState.Blocked,
            _ => ArtifactReadinessState.Ready
        };
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "dashboards_reports.readiness.mark",
                AuditResult.Success,
                null,
                $"Dashboard/report version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkDashboardReportReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes);
    }

    public async Task<DashboardReportExportFileResult> ExportAsync(
        string artifactType,
        Guid artifactId,
        Guid versionId,
        DashboardReportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("dashboards_reports.export", cancellationToken);
        var hasExportPermission = await HasExportPermissionAsync(context, cancellationToken);
        if (!hasExportPermission)
        {
            await RecordExportDeniedAsync(context, artifactId, versionId, cancellationToken);
            throw new TenantAccessDeniedException("User lacks dashboard/report export permission.");
        }

        var includeSensitive = await HasAdminPermissionAsync(context, cancellationToken);
        var template = await GetTemplateAsync(artifactType, artifactId, versionId, cancellationToken);
        var preview = await BuildPreviewAsync(template, request, cancellationToken);
        var policyVersion = await ResolvePolicyVersionAsync(preview.FilterSummary.PolicyKey, context.TenantId, cancellationToken);
        var export = DashboardReportExportBuilder.BuildExport(template, preview, context.UserId, includeSensitive, policyVersion);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "dashboards_reports.export",
                AuditResult.Export,
                null,
                $"Dashboard/report '{artifactId}' version '{versionId}' exported with evidence level '{export.Metadata.EvidenceLevel}'.",
                nameof(ArtifactVersion),
                versionId.ToString(),
                preview.FilterSummary.PolicyKey,
                policyVersion,
                RetentionCategory: AuditRetentionCategory.Export,
                IsArchiveEligible: true),
            cancellationToken);

        var exportRecord = new DashboardReportExportRecord
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            ArtifactVersionId = versionId,
            ArtifactType = artifactType,
            ExportedByUserId = context.UserId,
            ExportHash = export.Metadata.ExportHash,
            RedactionMetadataJson = DashboardReportExportBuilder.SerializeRedactionMetadata(export.Metadata.RedactionMetadata),
            EvidenceLevel = export.Metadata.EvidenceLevel,
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.DashboardReportExportRecords.Add(exportRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        return export with
        {
            Metadata = export.Metadata with { ExportRecordId = exportRecord.Id }
        };
    }

    private async Task<DashboardReportPreviewResponse> BuildPreviewAsync(
        DashboardReportTemplateResponse template,
        DashboardReportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var startGraphNodeId = request.StartGraphNodeId ?? template.DefaultAnchor.StartGraphNodeId;
        var documentArtifactId = request.DocumentArtifactId ?? template.DefaultAnchor.DocumentArtifactId;
        var blocks = new List<PreviewBlockResponse>();
        var deniedTotal = 0;
        var allowedTotal = 0;
        var governedQueryBlocks = 0;

        foreach (var block in template.Blocks)
        {
            switch (block.Kind)
            {
                case DashboardReportBlockKinds.GovernedQuery:
                    governedQueryBlocks++;
                    var queryBlock = await BuildGovernedQueryBlockAsync(
                        block,
                        startGraphNodeId,
                        documentArtifactId,
                        request.PolicyKey,
                        cancellationToken);
                    blocks.Add(queryBlock);
                    deniedTotal += queryBlock.DeniedCount;
                    allowedTotal += queryBlock.AllowedCount;
                    break;
                case DashboardReportBlockKinds.GovernanceKpiPlaceholder:
                    blocks.Add(BuildKpiPlaceholderBlock(block));
                    break;
                case DashboardReportBlockKinds.StaticText:
                    blocks.Add(new PreviewBlockResponse(
                        block.BlockId,
                        block.Title,
                        block.Kind,
                        block.StaticText ?? template.Summary ?? template.Name,
                        0,
                        0,
                        null,
                        null,
                        "ready"));
                    break;
                default:
                    throw new RequestValidationException($"Unsupported block kind '{block.Kind}'.");
            }
        }

        return new DashboardReportPreviewResponse(
            template.ArtifactId,
            template.VersionId,
            template.ArtifactType,
            template.VersionLabel,
            blocks,
            new PreviewFilterSummaryResponse(
                request.PolicyKey,
                blocks.Count,
                governedQueryBlocks,
                deniedTotal,
                allowedTotal));
    }

    private async Task<PreviewBlockResponse> BuildGovernedQueryBlockAsync(
        TemplateBlockResponse block,
        Guid? startGraphNodeId,
        Guid? documentArtifactId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var run = await governedQueryService.RunAsync(
            new RunGovernedQueryRequest(
                block.QueryIntentRef!,
                startGraphNodeId,
                documentArtifactId,
                policyKey,
                $"Dashboard/report preview block '{block.BlockId}'.",
                2,
                CreateAiTrace: false),
            cancellationToken);

        var package = run.ContextPackage;
        var summaries = package?.LlmVisibleContext
            .Select(item => item.SafeSummary)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(10)
            .ToList() ?? [];

        var safeSummary = summaries.Count > 0
            ? string.Join(" ", summaries)
            : run.SafeSummary;

        return new PreviewBlockResponse(
            block.BlockId,
            block.Title,
            block.Kind,
            Trim(safeSummary, 1200),
            package?.AllowedCount ?? 0,
            package?.DeniedCount ?? run.DeniedCount,
            block.QueryIntentRef,
            null,
            run.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? "ready" : run.Status);
    }

    private static PreviewBlockResponse BuildKpiPlaceholderBlock(TemplateBlockResponse block)
    {
        var placeholder = PlatformGovernanceKpiPlaceholders.Catalog
            .Single(item => item.KpiKey.Equals(block.KpiKey, StringComparison.OrdinalIgnoreCase));

        return new PreviewBlockResponse(
            block.BlockId,
            block.Title,
            block.Kind,
            $"{placeholder.Title}: {placeholder.Notes}",
            0,
            0,
            null,
            placeholder.KpiKey,
            "deferred");
    }

    private async Task<IReadOnlyCollection<DashboardReportArtifactSummaryResponse>> ListArtifactsByTypeAsync(
        string artifactType,
        CancellationToken cancellationToken)
    {
        await RequirePreviewPermissionAsync("dashboards_reports.artifacts.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("dashboards_reports.artifacts.list", cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Read, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "dashboards_reports.artifacts.list",
                "permission_denied",
                $"The user lacks the {ArtifactPermissions.Read} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks artifact read permission.");
        }

        var normalizedType = artifactType.ToUpperInvariant();
        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.NormalizedArtifactType == normalizedType)
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
            return new DashboardReportArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                artifact.UpdatedAt);
        }).ToList();
    }

    private async Task<(Artifact Artifact, ArtifactVersion Version)> RequireVersionAsync(
        string expectedArtifactType,
        Guid artifactId,
        Guid versionId,
        string action,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        if (!artifact.ArtifactType.Equals(expectedArtifactType, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"Artifact type '{artifact.ArtifactType}' does not match expected '{expectedArtifactType}'.");
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

    private async Task RequirePreviewPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await HasPreviewPermissionAsync(context, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {DashboardReportPermissions.Preview} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks dashboard/report preview permission.");
    }

    private async Task<ActiveTenantContext> RequireReadinessPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await HasReadinessPermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {DashboardReportPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks dashboard/report readiness permission.");
    }

    private async Task RecordExportDeniedAsync(ActiveTenantContext context, Guid artifactId, Guid versionId, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "dashboards_reports.export",
            "export_denied",
            $"The user lacks the {DashboardReportPermissions.Export} permission.",
            cancellationToken);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "dashboards_reports.export",
                AuditResult.Denied,
                "export_denied",
                $"Denied dashboard/report export for artifact '{artifactId}' version '{versionId}'.",
                nameof(ArtifactVersion),
                versionId.ToString(),
                RetentionCategory: AuditRetentionCategory.Security,
                IsArchiveEligible: true),
            cancellationToken);

        await auditRecorder.RecordSecurityEventAsync(
            new SecurityEventWriteRequest(
                context.TenantId,
                context.UserId,
                SecurityEventType.ExportDenied,
                SecurityEventSeverity.Medium,
                "dashboards_reports.export",
                "export_denied",
                $"Denied dashboard/report export for artifact '{artifactId}' version '{versionId}'.",
                audit.Id,
                ReviewTaskReady: true,
                ReviewTaskHint: "Review repeated dashboard/report export denials."),
            cancellationToken);
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", "Record belongs to a different tenant.", cancellationToken);
        throw new TenantAccessDeniedException("Record is not available in the active tenant.");
    }

    private async Task<bool> HasPreviewPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DashboardReportPermissions.Preview, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DashboardReportPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasExportPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DashboardReportPermissions.Export, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DashboardReportPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private async Task<string?> ResolvePolicyVersionAsync(string? policyKey, Guid tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return null;
        }

        var policy = await dbContext.PolicyVersions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PolicyKey == policyKey)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.VersionLabel)
            .FirstOrDefaultAsync(cancellationToken);

        return policy;
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
