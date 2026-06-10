using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ETOS.Backend.GraphMemory;

public sealed class Neo4jGraphBootstrapService(
    IDriver driver,
    IOptions<GraphMemoryOptions> options) : IGraphBootstrapService
{
    private static readonly string[] BootstrapStatements =
    [
        "CREATE CONSTRAINT etos_base_node_node_id IF NOT EXISTS FOR (node:BaseNode) REQUIRE node.nodeId IS UNIQUE",
        "CREATE INDEX etos_base_node_tenant IF NOT EXISTS FOR (node:BaseNode) ON (node.tenantId)",
        "CREATE INDEX etos_base_node_tenant_space_type IF NOT EXISTS FOR (node:BaseNode) ON (node.tenantId, node.graphSpace, node.objectType)",
        "CREATE INDEX etos_base_node_tenant_trust IF NOT EXISTS FOR (node:BaseNode) ON (node.tenantId, node.trustState)",
        "CREATE INDEX etos_base_relationship_id IF NOT EXISTS FOR ()-[relationship:BASE_RELATIONSHIP]-() ON (relationship.relationshipId)",
        "CREATE INDEX etos_base_relationship_tenant_type IF NOT EXISTS FOR ()-[relationship:BASE_RELATIONSHIP]-() ON (relationship.tenantId, relationship.relationshipType)",
        "CREATE INDEX etos_base_relationship_tenant_trust IF NOT EXISTS FOR ()-[relationship:BASE_RELATIONSHIP]-() ON (relationship.tenantId, relationship.trustState)"
    ];

    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        var database = options.Value.Neo4j.Database;
        await using var session = driver.AsyncSession(builder => builder.WithDatabase(database));

        foreach (var statement in BootstrapStatements)
        {
            var cursor = await session.RunAsync(statement);
            await cursor.ConsumeAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
