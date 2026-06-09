using ETOS.Backend.Identity;

namespace ETOS.Backend.Artifacts;

public static class ArtifactEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/artifacts")
            .RequireAuthorization()
            .WithTags("Artifacts");

        group.MapGet("/", async (IArtifactRegistryService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListArtifactsAsync(cancellationToken)));

        group.MapPost("/", async (
            CreateArtifactRequest request,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateArtifactAsync(request, cancellationToken)));

        group.MapGet("/{artifactId:guid}", async (
            Guid artifactId,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetArtifactAsync(artifactId, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions", async (
            Guid artifactId,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListVersionsAsync(artifactId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions", async (
            Guid artifactId,
            CreateArtifactVersionRequest request,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateVersionAsync(artifactId, request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/relationships", async (
            Guid artifactId,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListRelationshipsAsync(artifactId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/relationships", async (
            Guid artifactId,
            CreateArtifactRelationshipRequest request,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddRelationshipAsync(artifactId, request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}/dependencies", async (
            Guid artifactId,
            Guid versionId,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListDependenciesAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/dependencies", async (
            Guid artifactId,
            Guid versionId,
            CreateArtifactDependencyRequest request,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddDependencyAsync(artifactId, versionId, request, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}/impact", async (
            Guid artifactId,
            Guid versionId,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetImpactAsync(artifactId, versionId, cancellationToken)));

        group.MapGet("/{artifactId:guid}/versions/{versionId:guid}/readiness", async (
            Guid artifactId,
            Guid versionId,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetReadinessAsync(artifactId, versionId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/versions/{versionId:guid}/publish", async (
            Guid artifactId,
            Guid versionId,
            PublishArtifactVersionRequest request,
            IArtifactRegistryService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishVersionAsync(artifactId, versionId, request, cancellationToken)));

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
