namespace ETOS.Backend.AgentRuntime;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";

    public string? BaseUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}
