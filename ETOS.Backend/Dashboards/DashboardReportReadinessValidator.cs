namespace ETOS.Backend.Dashboards;

public static class DashboardReportReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateForReady(DashboardReportTemplateResponse template)
    {
        var notes = new List<string>();

        foreach (var block in template.Blocks.Where(item => item.Kind == DashboardReportBlockKinds.GovernedQuery))
        {
            if (RequiresGraphAnchor(block.QueryIntentRef!)
                && template.DefaultAnchor.StartGraphNodeId is null
                && template.DefaultAnchor.DocumentArtifactId is null)
            {
                notes.Add(
                    $"Block '{block.BlockId}' requires defaultAnchor.startGraphNodeId or defaultAnchor.documentArtifactId for intent '{block.QueryIntentRef}'.");
            }
        }

        return notes;
    }

    private static bool RequiresGraphAnchor(string queryIntentRef)
        => queryIntentRef.Equals("object-360-context", StringComparison.OrdinalIgnoreCase)
            || queryIntentRef.Equals("bom-impact-context", StringComparison.OrdinalIgnoreCase)
            || queryIntentRef.Equals("document-evidence-context", StringComparison.OrdinalIgnoreCase);
}
