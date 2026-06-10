using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ETOS.Backend.GraphMemory;

public sealed class Neo4jGraphDriverFactory(IOptions<GraphMemoryOptions> options)
{
    public IDriver CreateDriver()
    {
        var neo4j = options.Value.Neo4j;

        if (!neo4j.Enabled)
        {
            throw new InvalidOperationException("Neo4j graph memory is disabled.");
        }

        return GraphDatabase.Driver(neo4j.Uri, AuthTokens.Basic(neo4j.Username, neo4j.Password));
    }
}
