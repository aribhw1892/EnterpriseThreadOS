namespace ETOS.Backend.AgentRuns;

public static class AgentRunPermissions
{
    public const string Read = "agent-runs.read";
}

public static class AgentPermissions
{
    public const string Read = "agents.read";
    public const string Create = "agents.create";
    public const string Readiness = "agents.readiness";
    public const string Admin = "agents.admin";
    public const string Test = "agents.test";
    public const string Execute = "agents.execute";
}

public static class AgentRunStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Blocked = "Blocked";
    public const string PreviewSucceeded = "PreviewSucceeded";
    public const string SafeModeBlocked = "SafeModeBlocked";
}
