using ETOS.Backend.Identity;
using ETOS.Backend.Packages;

namespace ETOS.Backend.Platform.Development;

public static class DevelopmentEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadDevelopmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/development")
            .RequireAuthorization()
            .WithTags("Development");

        group.MapPost("/clean-demo-data", async (
            IDevelopmentDemoDataCleaner cleaner,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => cleaner.CleanTenantDemoDataAsync(cancellationToken)));

        group.MapPost("/install-reference-package", async (
            InstallReferencePackageRequest request,
            IReferencePackageInstaller installer,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => installer.InstallAsync(request, cancellationToken)));

        group.MapGet("/reference-packages/{packageKey}/demo-imports/{importName}", (
            string packageKey,
            string importName,
            IReferencePackageManifestLoader manifestLoader) =>
        {
            try
            {
                var loaded = manifestLoader.Load(packageKey);
                var relativePath = importName switch
                {
                    "flat-part-import" => loaded.Manifest.DemoImports.FlatPartImportFile,
                    "bom-comparison" => loaded.Manifest.DemoImports.BomComparisonFile,
                    _ => throw new RequestValidationException($"Unknown demo import name '{importName}'.")
                };
                var csv = manifestLoader.ReadPackageFile(packageKey, relativePath);
                return Results.Text(csv, "text/csv");
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
        });

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
