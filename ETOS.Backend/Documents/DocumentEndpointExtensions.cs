using ETOS.Backend.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ETOS.Backend.Documents;

public static class DocumentEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/documents")
            .RequireAuthorization()
            .WithTags("Documents");

        group.MapGet("/", async (
            string? policyKey,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListDocumentsAsync(policyKey, cancellationToken)));

        group.MapPost("/", async (
            CreateDocumentArtifactRequest request,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateDocumentAsync(request, cancellationToken)));

        group.MapGet("/{documentId:guid}", async (
            Guid documentId,
            string? policyKey,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetDocumentAsync(documentId, policyKey, cancellationToken)));

        group.MapPost("/{documentId:guid}/versions", async (
            Guid documentId,
            [FromForm] IFormFile file,
            [FromForm] string versionLabel,
            [FromForm] string? extractedMetadataSummaryJson,
            [FromForm] DocumentExtractionStatus extractionStatus,
            [FromForm] string? extractionFailureSummary,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddVersionAsync(
                documentId,
                file,
                new CreateDocumentVersionRequest(versionLabel, extractedMetadataSummaryJson, extractionStatus, extractionFailureSummary),
                cancellationToken)))
            .DisableAntiforgery();

        group.MapGet("/{documentId:guid}/links", async (
            Guid documentId,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListLinksAsync(documentId, cancellationToken)));

        group.MapPost("/{documentId:guid}/links", async (
            Guid documentId,
            CreateDocumentObjectLinkRequest request,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateLinkAsync(documentId, request, cancellationToken)));

        group.MapPost("/{documentId:guid}/versions/{versionId:guid}/extraction-issue", async (
            Guid documentId,
            Guid versionId,
            CreateDocumentExtractionIssueRequest request,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateExtractionIssueAsync(documentId, versionId, request, cancellationToken)));

        group.MapPost("/{documentId:guid}/versions/{versionId:guid}/vector-index", async (
            Guid documentId,
            Guid versionId,
            CreateDocumentVectorIndexRequest request,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RequestVectorIndexAsync(documentId, versionId, request, cancellationToken)));

        group.MapGet("/cad-parsing", async (
            IDocumentService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetCadParsingStatusAsync(cancellationToken)));

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
