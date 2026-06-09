using ETOS.Backend.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace ETOS.Backend.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void InfrastructureOptionsBindLocalEndpoints()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure:PostgreSql:Name"] = "PostgreSQL",
                ["Infrastructure:PostgreSql:Host"] = "localhost",
                ["Infrastructure:PostgreSql:Port"] = "5432",
                ["Infrastructure:Memgraph:Name"] = "Memgraph",
                ["Infrastructure:Memgraph:Host"] = "localhost",
                ["Infrastructure:Memgraph:Port"] = "7687",
                ["Infrastructure:Qdrant:Name"] = "Qdrant",
                ["Infrastructure:Qdrant:Host"] = "localhost",
                ["Infrastructure:Qdrant:Port"] = "6333",
                ["Infrastructure:Minio:Name"] = "MinIO",
                ["Infrastructure:Minio:Host"] = "localhost",
                ["Infrastructure:Minio:Port"] = "9000",
                ["Infrastructure:Redis:Name"] = "Redis",
                ["Infrastructure:Redis:Host"] = "localhost",
                ["Infrastructure:Redis:Port"] = "6379",
                ["Infrastructure:RabbitMq:Name"] = "RabbitMQ",
                ["Infrastructure:RabbitMq:Host"] = "localhost",
                ["Infrastructure:RabbitMq:Port"] = "5672"
            })
            .Build();

        var options = configuration.GetSection(InfrastructureHealthOptions.SectionName).Get<InfrastructureHealthOptions>();

        Assert.NotNull(options);
        Assert.Equal(5432, options.PostgreSql.Port);
        Assert.Equal(7687, options.Memgraph.Port);
        Assert.Equal(6333, options.Qdrant.Port);
        Assert.Equal(9000, options.Minio.Port);
        Assert.Equal(6379, options.Redis.Port);
        Assert.Equal(5672, options.RabbitMq.Port);
    }

    [Fact]
    public void OperationalStoreOptionsBindConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OperationalStore:ConnectionString"] = "Host=localhost;Database=etos;Username=etos;Password=local"
            })
            .Build();

        var options = configuration.GetSection(OperationalStoreOptions.SectionName).Get<OperationalStoreOptions>();

        Assert.NotNull(options);
        Assert.Contains("Database=etos", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }
}
