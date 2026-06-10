using System.ComponentModel.DataAnnotations;

namespace ETOS.Backend.GraphMemory;

public sealed class GraphMemoryOptions
{
    public const string SectionName = "GraphMemory";

    [Required]
    public string Provider { get; init; } = GraphMemoryProviderNames.Neo4j;

    [Required]
    public Neo4jGraphMemoryOptions Neo4j { get; init; } = new();

    [Required]
    public MemgraphGraphMemoryOptions Memgraph { get; init; } = new();
}

public sealed class Neo4jGraphMemoryOptions
{
    [Required]
    public string Uri { get; init; } = "bolt://localhost:7687";

    [Required]
    public string Username { get; init; } = "neo4j";

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string Database { get; init; } = "neo4j";

    public bool Enabled { get; init; } = true;

    public bool BootstrapOnStartup { get; init; } = true;
}

public sealed class MemgraphGraphMemoryOptions
{
    public bool Enabled { get; init; }
}

public static class GraphMemoryProviderNames
{
    public const string Neo4j = "Neo4j";
    public const string Memgraph = "Memgraph";
}
