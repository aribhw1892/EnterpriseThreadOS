using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Agents;

public static class AgentDefinitionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadAgentDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/agents")
            .RequireAuthorization()
            .WithTags("Agents");

        group.MapGet("/", async (
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapPost("/", async (
            CreateAgentDefinitionRequest request,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAsync(request, cancellationToken)));

        group.MapPost("/from-template", async (
            CreateAgentFromTemplateRequest request,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateFromTemplateAsync(request, cancellationToken)));

        group.MapPost("/from-prompt", async (
            CreateAgentFromPromptRequest request,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateFromPromptAsync(request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}/dependencies", async (
            Guid artifactId,
            Guid versionId,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetDependenciesAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions", async (
            Guid artifactId,
            CreateAgentDefinitionVersionRequest request,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateVersionAsync(artifactId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/publish", async (
            Guid artifactId,
            Guid versionId,
            PublishArtifactVersionRequest request,
            IAgentDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishAsync(artifactId, versionId, request, cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (RequestValidationException exception)
        {
            return Results.BadRequest(new ProblemResponse(exception.Message));
        }
        catch (TenantAccessDeniedException exception)
        {
            return Results.Problem(
                title: "Forbidden",
                detail: exception.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
