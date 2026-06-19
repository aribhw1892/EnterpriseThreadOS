using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class ConnectorDefinitionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadConnectorDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/connectors")
            .RequireAuthorization()
            .WithTags("Connectors");

        group.MapGet("/", async (
            IConnectorDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapPost("/", async (
            CreateConnectorDefinitionRequest request,
            IConnectorDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAsync(request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IConnectorDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions", async (
            Guid artifactId,
            CreateConnectorDefinitionVersionRequest request,
            IConnectorDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateVersionAsync(artifactId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IConnectorDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/publish", async (
            Guid artifactId,
            Guid versionId,
            PublishArtifactVersionRequest request,
            IConnectorDefinitionService service,
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
