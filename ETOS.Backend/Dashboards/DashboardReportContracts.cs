namespace ETOS.Backend.Dashboards;

public static class DashboardReportPermissions
{
    public const string Preview = "dashboards_reports.preview";
    public const string Export = "dashboards_reports.export";
    public const string Readiness = "dashboards_reports.readiness";
    public const string Admin = "dashboards_reports.admin";
}

public static class DashboardReportArtifactTypes
{
    public const string Dashboard = "DashboardVersion";
    public const string Report = "ReportVersion";
}

public static class DashboardReportBlockKinds
{
    public const string GovernedQuery = "governed_query";
    public const string GovernanceKpiPlaceholder = "governance_kpi_placeholder";
    public const string StaticText = "static_text";
}

public static class PlatformGovernanceKpiPlaceholders
{
    public static readonly IReadOnlyCollection<GovernanceKpiPlaceholderResponse> Catalog =
    [
        new("open_reviews", "Open Reviews", "Milestone 4 governance analytics.", "platform_defined"),
        new("pending_decisions", "Pending Decisions", "Milestone 4 governance analytics.", "platform_defined"),
        new("blocked_decisions", "Blocked Decisions", "Milestone 4 governance analytics.", "platform_defined"),
        new("escalations", "Escalations", "Milestone 4 governance analytics.", "platform_defined"),
        new("decision_throughput", "Decision Throughput", "Milestone 4 governance analytics.", "platform_defined"),
        new("outcome_verification_rate", "Outcome Verification Rate", "Milestone 4 governance analytics.", "platform_defined"),
        new("learning_signal_rate", "Learning Signal Rate", "Milestone 4 governance analytics.", "platform_defined"),
        new("high_risk_recommendations", "High-Risk Recommendations", "Milestone 4 governance analytics.", "platform_defined"),
        new("tenant_custom_kpi", "Custom KPI (future)", "Tenant-defined KPI definitions deferred.", "tenant_custom_deferred")
    ];

    public static readonly IReadOnlySet<string> AllowedIntentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "object-360-context",
        "bom-impact-context",
        "document-evidence-context"
    };
}

public sealed record GovernanceKpiPlaceholderResponse(
    string KpiKey,
    string Title,
    string Notes,
    string Source);

public sealed record DashboardReportArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    DateTimeOffset UpdatedAt);

public sealed record TemplateAnchorResponse(
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId);

public sealed record TemplateBlockResponse(
    string BlockId,
    string Title,
    string Kind,
    string? QueryIntentRef,
    string? Visualization,
    string? KpiKey,
    string? StaticText);

public sealed record DashboardReportTemplateResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ArtifactType,
    string VersionLabel,
    string Name,
    string? Summary,
    TemplateAnchorResponse DefaultAnchor,
    IReadOnlyCollection<TemplateBlockResponse> Blocks);

public sealed record DashboardReportPreviewRequest(
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    string? PolicyKey);

public sealed record PreviewBlockResponse(
    string BlockId,
    string Title,
    string Kind,
    string SafeSummary,
    int AllowedCount,
    int DeniedCount,
    string? QueryIntentRef,
    string? KpiKey,
    string Status);

public sealed record PreviewFilterSummaryResponse(
    string? PolicyKey,
    int TotalBlocks,
    int GovernedQueryBlocks,
    int DeniedContextTotal,
    int AllowedContextTotal);

public sealed record DashboardReportPreviewResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ArtifactType,
    string VersionLabel,
    IReadOnlyCollection<PreviewBlockResponse> Blocks,
    PreviewFilterSummaryResponse FilterSummary);

public sealed record MarkDashboardReportReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record DashboardReportExportRedactionMetadataResponse(
    string? PolicyKey,
    string? PolicyVersion,
    IReadOnlyCollection<string> RedactedCategories,
    string EvidenceLevel,
    DateTimeOffset ExportedAt,
    Guid ExportedByUserId);

public sealed record DashboardReportExportMetadataResponse(
    Guid ExportRecordId,
    Guid ArtifactId,
    Guid VersionId,
    string ExportHash,
    string EvidenceLevel,
    DashboardReportExportRedactionMetadataResponse RedactionMetadata,
    DateTimeOffset ExportedAt);

public sealed record DashboardReportExportFileResult(
    byte[] Content,
    string FileName,
    string ContentType,
    DashboardReportExportMetadataResponse Metadata);
