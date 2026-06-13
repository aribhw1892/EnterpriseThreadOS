using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ETOS.Backend.AiTrace;

public static class AiTraceExportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static AiTraceExportFileResult BuildExport(
        AiTraceDetailResponse trace,
        Guid exportedByUserId,
        bool includeSensitiveDeniedReferences,
        string? policyVersion)
    {
        var redactedCategories = new List<string>();
        if (!includeSensitiveDeniedReferences && trace.SensitiveDeniedReferences is { Count: > 0 })
        {
            redactedCategories.Add("sensitiveDeniedReferences");
        }

        var redactionMetadata = new AiTraceExportRedactionMetadataResponse(
            trace.ConfidenceImpact.PolicyKey,
            policyVersion,
            redactedCategories,
            includeSensitiveDeniedReferences ? "fullPermissionSafe" : "permissionSafe",
            DateTimeOffset.UtcNow,
            exportedByUserId);

        var payload = new
        {
            traceId = trace.Id,
            tenantId = trace.TenantId,
            traceKind = trace.TraceKind.ToString(),
            intentKey = trace.IntentKey,
            strategyKey = trace.StrategyKey,
            queryText = trace.QueryText,
            status = trace.Status,
            safeSummary = trace.SafeSummary,
            retrievalRunId = trace.RetrievalRunId,
            contextPackageId = trace.ContextPackageId,
            sourcesSummary = trace.SourcesSummary,
            filteredSummaries = trace.FilteredSummaries,
            deniedSafeSummaries = trace.DeniedSafeSummaries,
            sensitiveDeniedReferences = includeSensitiveDeniedReferences ? trace.SensitiveDeniedReferences : null,
            confidenceImpact = trace.ConfidenceImpact,
            promptTemplateVersionLabel = trace.PromptTemplateVersionLabel,
            outputSchemaVersionLabel = trace.OutputSchemaVersionLabel,
            generatedOutputJson = trace.GeneratedOutputJson,
            artifactLinks = trace.ArtifactLinks,
            redactionMetadata,
            exportedAt = redactionMetadata.ExportedAt,
            exportedByUserId
        };

        var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var exportHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var fileName = $"ai-trace-{trace.Id:N}.json";

        return new AiTraceExportFileResult(
            content,
            fileName,
            "application/json",
            new AiTraceExportResponse(
                Guid.Empty,
                trace.Id,
                exportHash,
                redactionMetadata.EvidenceLevel,
                redactionMetadata,
                redactionMetadata.ExportedAt));
    }

    public static string SerializeRedactionMetadata(AiTraceExportRedactionMetadataResponse metadata)
        => JsonSerializer.Serialize(metadata, JsonOptions);
}
