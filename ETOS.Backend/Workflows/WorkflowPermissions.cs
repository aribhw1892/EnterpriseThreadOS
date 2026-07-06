namespace ETOS.Backend.Workflows;

public static class WorkflowPermissions
{
    public const string Read = "workflows.read";
    public const string Create = "workflows.create";
    public const string Readiness = "workflows.readiness";
    public const string Admin = "workflows.admin";
    public const string Preview = "workflows.preview";
    public const string Execute = "workflows.execute";
}

public static class WorkflowRunPermissions
{
    public const string Read = "workflow-runs.read";
}
