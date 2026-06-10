using Microsoft.Extensions.Options;

namespace ETOS.Backend.GraphMemory;

public sealed class GraphBootstrapHostedService(
    IGraphBootstrapService bootstrapService,
    IOptions<GraphMemoryOptions> options,
    ILogger<GraphBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Neo4j.Enabled || !options.Value.Neo4j.BootstrapOnStartup)
        {
            return;
        }

        try
        {
            await bootstrapService.BootstrapAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Neo4j graph bootstrap did not complete. Ensure local graph infrastructure is running before using graph memory.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
