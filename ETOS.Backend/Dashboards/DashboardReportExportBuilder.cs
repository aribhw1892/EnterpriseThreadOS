using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ETOS.Backend.Dashboards;

public static class DashboardReportExportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static DashboardReportExportFileResult BuildExport(
        DashboardReportTemplateResponse template,
        DashboardReportPreviewResponse preview,
        Guid exportedByUserId,
        bool includeSensitiveDeniedReferences,
        string? policyVersion)
    {
        var redactedCategories = new List<string>();
        if (!includeSensitiveDeniedReferences)
        {
            redactedCategories.Add("sensitiveDeniedReferences");
        }

        var redactionMetadata = new DashboardReportExportRedactionMetadataResponse(
            preview.FilterSummary.PolicyKey,
            policyVersion,
            redactedCategories,
            includeSensitiveDeniedReferences ? "fullPermissionSafe" : "permissionSafe",
            DateTimeOffset.UtcNow,
            exportedByUserId);

        var payload = new
        {
            artifactId = template.ArtifactId,
            versionId = template.VersionId,
            artifactType = template.ArtifactType,
            versionLabel = template.VersionLabel,
            template,
            preview,
            redactionMetadata,
            exportedAt = redactionMetadata.ExportedAt,
            exportedByUserId
        };

        var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var exportHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var prefix = template.ArtifactType == DashboardReportArtifactTypes.Report ? "report" : "dashboard";

        return new DashboardReportExportFileResult(
            content,
            $"{prefix}-{template.ArtifactId:N}.json",
            "application/json",
            new DashboardReportExportMetadataResponse(
                Guid.Empty,
                template.ArtifactId,
                template.VersionId,
                exportHash,
                redactionMetadata.EvidenceLevel,
                redactionMetadata,
                redactionMetadata.ExportedAt));
    }

    public static string SerializeRedactionMetadata(DashboardReportExportRedactionMetadataResponse metadata)
        => JsonSerializer.Serialize(metadata, JsonOptions);
}
