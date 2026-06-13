using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Dashboards;

public static class DashboardReportTemplateParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static DashboardReportTemplateResponse Parse(
        string artifactType,
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<TemplatePayload>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Dashboard or report template payload is invalid.");

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            throw new RequestValidationException("Template name is required.");
        }

        var blocks = artifactType == DashboardReportArtifactTypes.Report
            ? ParseReportSections(payload.Sections)
            : ParseDashboardWidgets(payload.Widgets);

        if (blocks.Count == 0)
        {
            throw new RequestValidationException("Template must contain at least one widget or section.");
        }

        return new DashboardReportTemplateResponse(
            artifactId,
            versionId,
            artifactType,
            versionLabel,
            payload.Name.Trim(),
            payload.Summary?.Trim(),
            new TemplateAnchorResponse(
                payload.DefaultAnchor?.StartGraphNodeId,
                payload.DefaultAnchor?.DocumentArtifactId),
            blocks);
    }

    private static IReadOnlyCollection<TemplateBlockResponse> ParseDashboardWidgets(IReadOnlyCollection<TemplateBlockPayload>? widgets)
    {
        if (widgets is null || widgets.Count == 0)
        {
            return [];
        }

        return widgets.Select(ParseBlock).ToList();
    }

    private static IReadOnlyCollection<TemplateBlockResponse> ParseReportSections(IReadOnlyCollection<TemplateBlockPayload>? sections)
    {
        if (sections is null || sections.Count == 0)
        {
            return [];
        }

        return sections.Select(block =>
        {
            var parsed = ParseBlock(block);
            var blockId = string.IsNullOrWhiteSpace(block.SectionId) ? parsed.BlockId : block.SectionId.Trim();
            return parsed with { BlockId = blockId };
        }).ToList();
    }

    private static TemplateBlockResponse ParseBlock(TemplateBlockPayload block)
    {
        var blockId = string.IsNullOrWhiteSpace(block.WidgetId)
            ? string.IsNullOrWhiteSpace(block.SectionId) ? Guid.NewGuid().ToString("N") : block.SectionId.Trim()
            : block.WidgetId.Trim();
        var title = string.IsNullOrWhiteSpace(block.Title) ? blockId : block.Title.Trim();
        var kind = string.IsNullOrWhiteSpace(block.Kind)
            ? InferKind(block)
            : block.Kind.Trim().ToLowerInvariant();

        ValidateBlockKind(kind, block, blockId);

        return new TemplateBlockResponse(
            blockId,
            title,
            kind,
            block.QueryIntentRef?.Trim(),
            block.Visualization?.Trim(),
            block.KpiKey?.Trim(),
            block.StaticText?.Trim());
    }

    private static string InferKind(TemplateBlockPayload block)
    {
        if (!string.IsNullOrWhiteSpace(block.KpiKey))
        {
            return DashboardReportBlockKinds.GovernanceKpiPlaceholder;
        }

        if (!string.IsNullOrWhiteSpace(block.QueryIntentRef))
        {
            return DashboardReportBlockKinds.GovernedQuery;
        }

        return DashboardReportBlockKinds.StaticText;
    }

    private static void ValidateBlockKind(string kind, TemplateBlockPayload block, string blockId)
    {
        switch (kind)
        {
            case DashboardReportBlockKinds.GovernedQuery:
                if (string.IsNullOrWhiteSpace(block.QueryIntentRef))
                {
                    throw new RequestValidationException($"Widget '{blockId}' requires queryIntentRef.");
                }

                if (!PlatformGovernanceKpiPlaceholders.AllowedIntentKeys.Contains(block.QueryIntentRef.Trim()))
                {
                    throw new RequestValidationException(
                        $"Widget '{blockId}' uses unsupported queryIntentRef '{block.QueryIntentRef}'.");
                }

                break;
            case DashboardReportBlockKinds.GovernanceKpiPlaceholder:
                if (string.IsNullOrWhiteSpace(block.KpiKey))
                {
                    throw new RequestValidationException($"Widget '{blockId}' requires kpiKey.");
                }

                if (PlatformGovernanceKpiPlaceholders.Catalog.All(item => !item.KpiKey.Equals(block.KpiKey.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new RequestValidationException($"Widget '{blockId}' uses unknown kpiKey '{block.KpiKey}'.");
                }

                break;
            case DashboardReportBlockKinds.StaticText:
                break;
            default:
                throw new RequestValidationException($"Widget '{blockId}' uses unsupported kind '{kind}'.");
        }
    }

    private sealed class TemplatePayload
    {
        public string Name { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public TemplateAnchorPayload? DefaultAnchor { get; set; }
        public List<TemplateBlockPayload>? Widgets { get; set; }
        public List<TemplateBlockPayload>? Sections { get; set; }
    }

    private sealed class TemplateAnchorPayload
    {
        public Guid? StartGraphNodeId { get; set; }
        public Guid? DocumentArtifactId { get; set; }
    }

    private sealed class TemplateBlockPayload
    {
        public string? WidgetId { get; set; }
        public string? SectionId { get; set; }
        public string? Title { get; set; }
        public string? Kind { get; set; }
        public string? QueryIntentRef { get; set; }
        public string? Visualization { get; set; }
        public string? KpiKey { get; set; }
        public string? StaticText { get; set; }
    }
}
