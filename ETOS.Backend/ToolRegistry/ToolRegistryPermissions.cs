namespace ETOS.Backend.ToolRegistry;

public static class ToolDefinitionPermissions
{
    public const string Read = "tools.read";
    public const string Create = "tools.create";
    public const string Readiness = "tools.readiness";
    public const string Admin = "tools.admin";
    public const string Execute = "tools.execute";
    public const string DryRun = "tools.dry_run";
}

public static class SkillDefinitionPermissions
{
    public const string Read = "skills.read";
    public const string Create = "skills.create";
    public const string Readiness = "skills.readiness";
    public const string Admin = "skills.admin";
}

public static class ConnectorDefinitionPermissions
{
    public const string Read = "connectors.read";
    public const string Create = "connectors.create";
    public const string Readiness = "connectors.readiness";
    public const string Admin = "connectors.admin";
}

public static class ToolRunPermissions
{
    public const string Read = "tool-runs.read";
}

public static class ToolDefinitionArtifactTypes
{
    public const string ToolDefinition = "ToolDefinitionVersion";
}

public static class SkillDefinitionArtifactTypes
{
    public const string SkillDefinition = "SkillDefinitionVersion";
}

public static class ConnectorDefinitionArtifactTypes
{
    public const string ConnectorDefinition = "ConnectorDefinitionVersion";
}

public static class ToolRiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    public static readonly IReadOnlyCollection<string> All = [Low, Medium, High];
}

public static class ToolInternalHandlerKeys
{
    public const string GovernedQuery = "governed-query-v1";
    public const string DisabledWriteConnector = "disabled-write-connector-v1";

    public static readonly IReadOnlyCollection<string> All =
    [
        GovernedQuery,
        DisabledWriteConnector
    ];
}

public static class ConnectorKinds
{
    public const string Read = "Read";
    public const string Write = "Write";
    public const string Action = "Action";

    public static readonly IReadOnlyCollection<string> All = [Read, Write, Action];
}

public static class ToolRunStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string DryRunSucceeded = "DryRunSucceeded";
    public const string Blocked = "Blocked";
}
