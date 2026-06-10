using System.Diagnostics;
using System.Net.Sockets;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Infrastructure.Configuration;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Health;

public sealed class InfrastructureHealthService(
    IServiceScopeFactory scopeFactory,
    HttpClient httpClient,
    IGraphHealthService graphHealthService,
    IOptions<InfrastructureHealthOptions> options) : IInfrastructureHealthService
{
    public async Task<IReadOnlyCollection<ComponentHealthResponse>> CheckAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(settings.ProbeTimeoutMilliseconds));

        var checks = new[]
        {
            CheckPostgreSqlAsync(settings.PostgreSql.Name, timeoutSource.Token),
            CheckGraphHealthAsync(timeoutSource.Token),
            CheckHttpOrTcpAsync(settings.Qdrant, timeoutSource.Token),
            CheckHttpOrTcpAsync(settings.Minio, timeoutSource.Token),
            CheckTcpAsync(settings.Redis, timeoutSource.Token),
            CheckHttpOrTcpAsync(settings.RabbitMq, timeoutSource.Token)
        };

        return await Task.WhenAll(checks);
    }

    private async Task<ComponentHealthResponse> CheckGraphHealthAsync(CancellationToken cancellationToken)
    {
        var graphHealth = await graphHealthService.CheckAsync(cancellationToken);

        return new ComponentHealthResponse(
            graphHealth.Provider,
            graphHealth.Status,
            graphHealth.Description,
            graphHealth.DurationMilliseconds);
    }

    private async Task<ComponentHealthResponse> CheckPostgreSqlAsync(string name, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            return new ComponentHealthResponse(
                name,
                canConnect ? "healthy" : "unhealthy",
                canConnect ? "EF Core connected to PostgreSQL." : "EF Core could not connect to PostgreSQL.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return Unhealthy(name, exception.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<ComponentHealthResponse> CheckHttpOrTcpAsync(EndpointOptions endpoint, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(endpoint.HealthUrl, UriKind.Absolute, out var healthUri))
        {
            return await CheckHttpAsync(endpoint.Name, healthUri, cancellationToken);
        }

        return await CheckTcpAsync(endpoint, cancellationToken);
    }

    private async Task<ComponentHealthResponse> CheckHttpAsync(string name, Uri healthUri, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync(healthUri, cancellationToken);
            stopwatch.Stop();

            return new ComponentHealthResponse(
                name,
                response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                $"HTTP probe returned {(int)response.StatusCode}.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return Unhealthy(name, exception.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task<ComponentHealthResponse> CheckTcpAsync(EndpointOptions endpoint, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
            stopwatch.Stop();

            return new ComponentHealthResponse(
                endpoint.Name,
                "healthy",
                $"TCP probe connected to {endpoint.Host}:{endpoint.Port}.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return Unhealthy(endpoint.Name, exception.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private static ComponentHealthResponse Unhealthy(string name, string description, long durationMilliseconds)
    {
        return new ComponentHealthResponse(name, "unhealthy", description, durationMilliseconds);
    }
}
