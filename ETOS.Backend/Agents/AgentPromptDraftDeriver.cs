using System.Text.RegularExpressions;

namespace ETOS.Backend.Agents;

internal static class AgentPromptDraftDeriver
{
    internal static string ExtractUserPrompt(string prompt)
    {
        const string marker = "User prompt:";
        var start = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return prompt.Trim();
        }

        start += marker.Length;
        return prompt[start..].Trim();
    }

    internal static string DeriveAgentKey(string userPrompt)
    {
        var normalized = StripAgentCreationPrefix(userPrompt.Trim());
        var slug = Regex.Replace(normalized.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0)
        {
            slug = $"prompt-agent-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        }

        return slug.Length <= 64 ? slug : slug[..64].Trim('-');
    }

    internal static string DeriveDisplayName(string userPrompt)
    {
        var normalized = StripAgentCreationPrefix(userPrompt.Trim());
        if (normalized.Length == 0)
        {
            return "Prompt draft agent";
        }

        var displayName = char.ToUpperInvariant(normalized[0]) + normalized[1..];
        return displayName.Length <= 120 ? displayName : displayName[..120].TrimEnd();
    }

    internal static string DeriveDescription(string userPrompt)
    {
        var normalized = userPrompt.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500].TrimEnd();
    }

    internal static string DerivePatternSummary(string userPrompt)
    {
        var normalized = StripAgentCreationPrefix(userPrompt.Trim());
        if (normalized.Length == 0)
        {
            return "Draft agent pattern created from a natural-language prompt.";
        }

        var summary = $"Draft agent pattern derived from: {normalized}";
        return summary.Length <= 240 ? summary : summary[..240].TrimEnd();
    }

    private static string StripAgentCreationPrefix(string value)
    {
        foreach (var prefix in new[]
        {
            "create an agent that ",
            "create an agent to ",
            "create a agent that ",
            "create a agent to ",
            "create an agent ",
            "create a ",
            "build an agent that ",
            "build an agent to ",
            "build an agent "
        })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[prefix.Length..].Trim().TrimEnd('.');
            }
        }

        return value.TrimEnd('.');
    }
}
