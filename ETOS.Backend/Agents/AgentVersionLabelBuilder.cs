namespace ETOS.Backend.Agents;

internal static class AgentVersionLabelBuilder
{
    internal static string NextVersionLabel(string currentLabel, IReadOnlyCollection<string> existingLabels)
    {
        var normalizedExisting = existingLabels
            .Select(label => label.Trim())
            .Where(label => label.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (Version.TryParse(currentLabel, out var parsed))
        {
            var nextBuild = parsed.Build >= 0 ? parsed.Build + 1 : 1;
            var candidate = new Version(parsed.Major, parsed.Minor, nextBuild).ToString();
            if (!normalizedExisting.Contains(candidate))
            {
                return candidate;
            }
        }

        var suffix = 2;
        while (normalizedExisting.Contains($"{currentLabel}-model-{suffix}", StringComparer.OrdinalIgnoreCase))
        {
            suffix++;
        }

        return $"{currentLabel}-model-{suffix}";
    }
}
