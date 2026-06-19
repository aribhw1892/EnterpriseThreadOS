using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentTypes;

public static class AgentTypeDefinitionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadAgentTypeDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/agent-types")
            .RequireAuthorization()
            .WithTags("AgentTypes");

        group.MapGet("/", async (
            IAgentTypeDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapPost("/", async (
            CreateAgentTypeDefinitionRequest request,
            IAgentTypeDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAsync(request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IAgentTypeDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions", async (
            Guid artifactId,
            CreateAgentTypeDefinitionVersionRequest request,
            IAgentTypeDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateVersionAsync(artifactId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IAgentTypeDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/publish", async (
            Guid artifactId,
            Guid versionId,
            PublishArtifactVersionRequest request,
            IAgentTypeDefinitionService service,
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
