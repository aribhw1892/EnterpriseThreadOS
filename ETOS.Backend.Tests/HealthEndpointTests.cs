using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Health;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task AppHealthEndpointReturnsEnvironment()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health/app");
        var health = await response.Content.ReadFromJsonAsync<AppHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("healthy", health.Status);
        Assert.False(string.IsNullOrWhiteSpace(health.Environment));
    }

    [Fact]
    public async Task PlatformHealthEndpointReturnsSafeComponentStatuses()
    {
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IInfrastructureHealthService>();
                    services.AddSingleton<IInfrastructureHealthService>(new StubInfrastructureHealthService());
                });
            });

        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/health");
        var health = await response.Content.ReadFromJsonAsync<PlatformHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("degraded", health.Status);
        Assert.Contains(health.Components, component => component.Name == "PostgreSQL");
        Assert.DoesNotContain(health.Components, component => component.Description?.Contains("Password", StringComparison.OrdinalIgnoreCase) == true);
    }

    private sealed class StubInfrastructureHealthService : IInfrastructureHealthService
    {
        public Task<IReadOnlyCollection<ComponentHealthResponse>> CheckAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ComponentHealthResponse> components =
            [
                new("PostgreSQL", "healthy", "Connected.", 1),
                new("RabbitMQ", "unhealthy", "Connection refused.", 1)
            ];

            return Task.FromResult(components);
        }
    }
}
