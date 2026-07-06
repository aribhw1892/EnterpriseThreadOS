using Dapr.Workflow;
using ETOS.Backend.WorkflowRuntime.Dapr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.WorkflowRuntime;

public static class WorkflowRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseThreadDaprWorkflow(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var workflowOptions = configuration
            .GetSection(WorkflowRuntimeOptions.SectionName)
            .Get<WorkflowRuntimeOptions>() ?? new WorkflowRuntimeOptions();

        if (!workflowOptions.EnableDaprHost)
        {
            return services;
        }

        services.AddDaprClient();
        services.AddDaprWorkflow(options =>
        {
            options.RegisterWorkflow<GovernedWorkflowOrchestrator>();
            options.RegisterActivity<ExecuteGovernedWorkflowStepActivity>();
        });

        return services;
    }
}
