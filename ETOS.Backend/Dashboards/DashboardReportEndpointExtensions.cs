using ETOS.Backend.Identity;

namespace ETOS.Backend.Dashboards;

public static class DashboardReportEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadDashboardReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var dashboardGroup = endpoints.MapGroup("/api/admin/dashboards")
            .RequireAuthorization()
            .WithTags("Dashboards");

        dashboardGroup.MapGet("/kpi-placeholders", async (
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListKpiPlaceholdersAsync(cancellationToken)));

        dashboardGroup.MapGet("/artifacts", async (
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListDashboardArtifactsAsync(cancellationToken)));

        dashboardGroup.MapGet("/{artifactId:guid}/versions/{versionId:guid}/template", async (
            Guid artifactId,
            Guid versionId,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetTemplateAsync(DashboardReportArtifactTypes.Dashboard, artifactId, versionId, cancellationToken)));

        dashboardGroup.MapPost("/{artifactId:guid}/versions/{versionId:guid}/preview", async (
            Guid artifactId,
            Guid versionId,
            DashboardReportPreviewRequest request,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PreviewAsync(DashboardReportArtifactTypes.Dashboard, artifactId, versionId, request, cancellationToken)));

        dashboardGroup.MapPost("/{artifactId:guid}/versions/{versionId:guid}/export", async (
            Guid artifactId,
            Guid versionId,
            DashboardReportPreviewRequest request,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteExportAsync(() => service.ExportAsync(DashboardReportArtifactTypes.Dashboard, artifactId, versionId, request, cancellationToken)));

        dashboardGroup.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(DashboardReportArtifactTypes.Dashboard, artifactId, versionId, cancellationToken)));

        var reportGroup = endpoints.MapGroup("/api/admin/reports")
            .RequireAuthorization()
            .WithTags("Reports");

        reportGroup.MapGet("/artifacts", async (
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListReportArtifactsAsync(cancellationToken)));

        reportGroup.MapGet("/{artifactId:guid}/versions/{versionId:guid}/template", async (
            Guid artifactId,
            Guid versionId,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetTemplateAsync(DashboardReportArtifactTypes.Report, artifactId, versionId, cancellationToken)));

        reportGroup.MapPost("/{artifactId:guid}/versions/{versionId:guid}/preview", async (
            Guid artifactId,
            Guid versionId,
            DashboardReportPreviewRequest request,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PreviewAsync(DashboardReportArtifactTypes.Report, artifactId, versionId, request, cancellationToken)));

        reportGroup.MapPost("/{artifactId:guid}/versions/{versionId:guid}/export", async (
            Guid artifactId,
            Guid versionId,
            DashboardReportPreviewRequest request,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteExportAsync(() => service.ExportAsync(DashboardReportArtifactTypes.Report, artifactId, versionId, request, cancellationToken)));

        reportGroup.MapPost("/{artifactId:guid}/versions/{versionId:guid}/mark-ready", async (
            Guid artifactId,
            Guid versionId,
            IDashboardReportService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.MarkReadyAsync(DashboardReportArtifactTypes.Report, artifactId, versionId, cancellationToken)));

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

    private static async Task<IResult> ExecuteExportAsync(Func<Task<DashboardReportExportFileResult>> action)
    {
        try
        {
            var export = await action();
            return Results.File(export.Content, export.ContentType, export.FileName);
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
