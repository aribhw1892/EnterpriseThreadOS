using ETOS.Backend.Platform.Extensions;

namespace ETOS.Backend.Health;

public static class HealthEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/app", (IHostEnvironment environment) =>
        {
            return Results.Ok(new AppHealthResponse("healthy", environment.EnvironmentName, DateTimeOffset.UtcNow));
        })
        .WithName("GetAppHealth");

        endpoints.MapGet("/health/infrastructure", async (
            IInfrastructureHealthService healthService,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var components = await healthService.CheckAsync(cancellationToken);
            return Results.Ok(ToPlatformHealthResponse(environment.EnvironmentName, components));
        })
        .WithName("GetInfrastructureHealth");

        endpoints.MapGet("/api/health", async (
            IInfrastructureHealthService healthService,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var components = await healthService.CheckAsync(cancellationToken);
            return Results.Ok(ToPlatformHealthResponse(environment.EnvironmentName, components));
        })
        .WithName("GetPlatformHealth");

        endpoints.MapGet("/api/platform/extensions", (IExtensionPointCatalog catalog) =>
        {
            return Results.Ok(catalog.List());
        })
        .WithName("GetExtensionPoints");

        return endpoints;
    }

    private static PlatformHealthResponse ToPlatformHealthResponse(
        string environment,
        IReadOnlyCollection<ComponentHealthResponse> components)
    {
        var status = components.All(component => component.Status == "healthy")
            ? "healthy"
            : "degraded";

        return new PlatformHealthResponse(status, environment, DateTimeOffset.UtcNow, components);
    }
}
