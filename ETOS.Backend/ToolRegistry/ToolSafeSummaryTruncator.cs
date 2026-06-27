namespace ETOS.Backend.ToolRegistry;

public static class ToolSafeSummaryTruncator
{
    public const int MaxLength = 1000;

    public static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxLength)
        {
            return value;
        }

        const string suffix = "...[truncated]";
        return value[..(MaxLength - suffix.Length)] + suffix;
    }
}
