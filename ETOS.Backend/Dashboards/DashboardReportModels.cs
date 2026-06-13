using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Dashboards;

public sealed class DashboardReportExportRecord : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid ArtifactVersionId { get; set; }
    public required string ArtifactType { get; set; }
    public Guid ExportedByUserId { get; set; }
    public required string ExportHash { get; set; }
    public required string RedactionMetadataJson { get; set; }
    public required string EvidenceLevel { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
