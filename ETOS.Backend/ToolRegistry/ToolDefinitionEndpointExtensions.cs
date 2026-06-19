using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class ToolDefinitionEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadToolDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/tools")
            .RequireAuthorization()
            .WithTags("Tools");

        group.MapGet("/", async (
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAsync(cancellationToken)));

        group.MapPost("/", async (
            CreateToolDefinitionRequest request,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAsync(request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}", async (
            Guid artifactId,
            Guid versionId,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAsync(artifactId, versionId, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}/dependencies", async (
            Guid artifactId,
            Guid versionId,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetDependenciesAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions", async (
            Guid artifactId,
            CreateToolDefinitionVersionRequest request,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateVersionAsync(artifactId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/publish", async (
            Guid artifactId,
            Guid versionId,
            PublishArtifactVersionRequest request,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/compatibility-scan", async (
            Guid artifactId,
            Guid versionId,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CompatibilityScanAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/dry-run", async (
            Guid artifactId,
            Guid versionId,
            ToolExecutionRequest request,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.DryRunAsync(artifactId, versionId, request, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/execute", async (
            Guid artifactId,
            Guid versionId,
            ToolExecutionRequest request,
            IToolDefinitionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ExecuteAsync(artifactId, versionId, request, cancellationToken)));

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
