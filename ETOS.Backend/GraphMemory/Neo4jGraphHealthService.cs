using System.Diagnostics;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ETOS.Backend.GraphMemory;

public sealed class Neo4jGraphHealthService(
    IDriver driver,
    IGraphBootstrapService bootstrapService,
    IOptions<GraphMemoryOptions> options) : IGraphHealthService
{
    public async Task<GraphHealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var bootstrapApplied = false;

        try
        {
            await driver.VerifyConnectivityAsync();

            if (options.Value.Neo4j.BootstrapOnStartup)
            {
                await bootstrapService.BootstrapAsync(cancellationToken);
                bootstrapApplied = true;
            }

            stopwatch.Stop();

            return new GraphHealthResponse(
                GraphMemoryProviderNames.Neo4j,
                "healthy",
                "Neo4j driver connected and graph bootstrap completed.",
                bootstrapApplied,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            return new GraphHealthResponse(
                GraphMemoryProviderNames.Neo4j,
                "unhealthy",
                exception.Message,
                bootstrapApplied,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
