using ETOS.Backend.Identity;

namespace ETOS.Backend.DataQuality;

public static class DataQualityEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadDataQualityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/data-quality")
            .RequireAuthorization()
            .WithTags("Data Quality");

        group.MapGet("/issues", async (
            IDataQualityIssueService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListIssuesAsync(cancellationToken)));

        group.MapGet("/issues/{issueId:guid}", async (
            Guid issueId,
            IDataQualityIssueService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetIssueAsync(issueId, cancellationToken)));

        group.MapPost("/issues", async (
            CreateDataQualityIssueRequest request,
            IDataQualityIssueService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateIssueAsync(request, cancellationToken)));

        group.MapPost("/imports/batches/{batchId:guid}/issues/generate", async (
            Guid batchId,
            IDataQualityIssueService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GenerateFromImportValidationAsync(batchId, cancellationToken)));

        group.MapPost("/security-events/{securityEventId:guid}/issues/create", async (
            Guid securityEventId,
            IDataQualityIssueService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateFromSecurityEventAsync(securityEventId, cancellationToken)));

        group.MapGet("/monitoring-placeholders", async (
            IDataQualityIssueService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListMonitoringPlaceholdersAsync(cancellationToken)));

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
