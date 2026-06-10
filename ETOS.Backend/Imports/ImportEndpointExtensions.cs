using ETOS.Backend.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ETOS.Backend.Imports;

public static class ImportEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/imports")
            .RequireAuthorization()
            .WithTags("Imports");

        group.MapGet("/batches", async (IImportService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListBatchesAsync(cancellationToken)));

        group.MapPost("/batches", async (
            CreateImportBatchRequest request,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateBatchAsync(request, cancellationToken)));

        group.MapGet("/batches/{batchId:guid}", async (
            Guid batchId,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetBatchAsync(batchId, cancellationToken)));

        group.MapPost("/batches/{batchId:guid}/files", async (
            Guid batchId,
            [FromForm] IFormFile file,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UploadFileAsync(batchId, file, cancellationToken)))
            .DisableAntiforgery();

        group.MapPost("/batches/{batchId:guid}/mapping-preview", async (
            Guid batchId,
            ImportPreviewRequest request,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PreviewMappingAsync(batchId, request, cancellationToken)));

        group.MapPost("/mappings", async (
            CreateImportMappingVersionRequest request,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateMappingVersionAsync(request, cancellationToken)));

        group.MapPost("/mappings/{mappingVersionId:guid}/approve", async (
            Guid mappingVersionId,
            ApproveImportMappingRequest request,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ApproveMappingVersionAsync(mappingVersionId, request, cancellationToken)));

        group.MapPost("/batches/{batchId:guid}/validate", async (
            Guid batchId,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ValidateBatchAsync(batchId, cancellationToken)));

        group.MapPost("/batches/{batchId:guid}/stage", async (
            Guid batchId,
            IImportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.StageBatchAsync(batchId, cancellationToken)));

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
