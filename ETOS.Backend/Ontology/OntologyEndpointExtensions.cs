using ETOS.Backend.Identity;

namespace ETOS.Backend.Ontology;

public static class OntologyEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadOntologyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/ontology")
            .RequireAuthorization()
            .WithTags("Ontology");

        group.MapGet("/versions", async (IOntologyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListOntologyVersionsAsync(cancellationToken)));

        group.MapGet("/versions/{versionId:guid}", async (
            Guid versionId,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetOntologyVersionAsync(versionId, cancellationToken)));

        group.MapPost("/versions", async (
            CreateOntologyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateOntologyVersionAsync(request, cancellationToken)));

        group.MapPost("/versions/{versionId:guid}/publish", async (
            Guid versionId,
            PublishOntologyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishOntologyVersionAsync(versionId, request, cancellationToken)));

        group.MapGet("/semantic-layers", async (IOntologyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListSemanticLayerVersionsAsync(cancellationToken)));

        group.MapPost("/semantic-layers", async (
            CreateSemanticLayerVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateSemanticLayerVersionAsync(request, cancellationToken)));

        group.MapPost("/semantic-layers/{versionId:guid}/publish", async (
            Guid versionId,
            PublishOntologyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishSemanticLayerVersionAsync(versionId, request, cancellationToken)));

        group.MapGet("/lifecycle-vocabularies", async (IOntologyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListLifecycleVocabularyVersionsAsync(cancellationToken)));

        group.MapGet("/lifecycle-vocabularies/{versionId:guid}", async (
            Guid versionId,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetLifecycleVocabularyVersionAsync(versionId, cancellationToken)));

        group.MapPost("/lifecycle-vocabularies", async (
            CreateLifecycleVocabularyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateLifecycleVocabularyVersionAsync(request, cancellationToken)));

        group.MapPost("/lifecycle-vocabularies/{versionId:guid}/publish", async (
            Guid versionId,
            PublishOntologyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishLifecycleVocabularyVersionAsync(versionId, request, cancellationToken)));

        group.MapGet("/attribute-schemas", async (IOntologyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAttributeSchemaVersionsAsync(cancellationToken)));

        group.MapGet("/attribute-schemas/{versionId:guid}", async (
            Guid versionId,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetAttributeSchemaVersionAsync(versionId, cancellationToken)));

        group.MapPost("/attribute-schemas", async (
            CreateAttributeSchemaVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAttributeSchemaVersionAsync(request, cancellationToken)));

        group.MapPost("/attribute-schemas/{versionId:guid}/publish", async (
            Guid versionId,
            PublishOntologyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishAttributeSchemaVersionAsync(versionId, request, cancellationToken)));

        group.MapGet("/model-packages", async (IOntologyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListModelPackageVersionsAsync(cancellationToken)));

        group.MapGet("/model-packages/active", async (
            string? key,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetActiveModelPackageAsync(key, cancellationToken)));

        group.MapGet("/model-packages/{versionId:guid}", async (
            Guid versionId,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetModelPackageVersionAsync(versionId, cancellationToken)));

        group.MapPost("/model-packages/preview", async (
            ModelPackagePreviewRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PreviewModelPackageAsync(request, cancellationToken)));

        group.MapPost("/model-packages", async (
            CreateModelPackageVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateModelPackageVersionAsync(request, cancellationToken)));

        group.MapPost("/model-packages/{versionId:guid}/publish", async (
            Guid versionId,
            PublishOntologyVersionRequest request,
            IOntologyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishModelPackageVersionAsync(versionId, request, cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            var response = await action();
            return Results.Ok(response);
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
