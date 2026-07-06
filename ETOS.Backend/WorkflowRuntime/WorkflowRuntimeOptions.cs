namespace ETOS.Backend.WorkflowRuntime;

public sealed class WorkflowRuntimeOptions
{
    public const string SectionName = "WorkflowRuntime";

    public string AdapterKey { get; set; } = WorkflowRuntimeAdapterKeys.InProcess;

    public bool EnableDaprHost { get; set; }

    public int CompletionTimeoutSeconds { get; set; } = 300;

    public string? DaprGrpcEndpoint { get; set; }

    public string? DaprAppId { get; set; }

    public string? DaprWorkflowComponent { get; set; }
}
