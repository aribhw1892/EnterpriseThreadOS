using System.ComponentModel.DataAnnotations;

namespace ETOS.Backend.Infrastructure.Configuration;

public sealed class InfrastructureHealthOptions
{
    public const string SectionName = "Infrastructure";

    [Required]
    public EndpointOptions PostgreSql { get; init; } = new();

    [Required]
    public EndpointOptions Neo4j { get; init; } = new();

    [Required]
    public EndpointOptions Qdrant { get; init; } = new();

    [Required]
    public EndpointOptions Minio { get; init; } = new();

    [Required]
    public EndpointOptions Redis { get; init; } = new();

    [Required]
    public EndpointOptions RabbitMq { get; init; } = new();

    [Range(50, 5000)]
    public int ProbeTimeoutMilliseconds { get; init; } = 750;
}

public sealed class EndpointOptions
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Host { get; init; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; init; }

    public string? HealthUrl { get; init; }
}
