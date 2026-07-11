using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.GraphMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace ETOS.Backend.Tests;

public sealed class GraphMemoryTests : IClassFixture<GraphMemoryTests.Neo4jFixture>
{
    private const string Neo4jUsername = "neo4j";
    private const string Neo4jPassword = "etos_neo4j_test_password";
    private readonly Neo4jFixture fixture;

    public GraphMemoryTests(Neo4jFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GraphHealthReportsHealthyAgainstNeo4jContainer()
    {
        await using var serviceProvider = CreateGraphServiceProvider(bootstrapOnStartup: true);
        var healthService = serviceProvider.GetRequiredService<IGraphHealthService>();

        var health = await healthService.CheckAsync(CancellationToken.None);

        Assert.Equal(GraphMemoryProviderNames.Neo4j, health.Provider);
        Assert.Equal("healthy", health.Status);
        Assert.True(health.BootstrapApplied);
    }

    [Fact]
    public async Task BootstrapIsIdempotent()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var bootstrapService = serviceProvider.GetRequiredService<IGraphBootstrapService>();

        await bootstrapService.BootstrapAsync(CancellationToken.None);
        await bootstrapService.BootstrapAsync(CancellationToken.None);

        var health = await serviceProvider.GetRequiredService<IGraphHealthService>().CheckAsync(CancellationToken.None);
        Assert.Equal("healthy", health.Status);
    }

    [Fact]
    public async Task CreateNodeRequiresTenantId()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var graphMemory = serviceProvider.GetRequiredService<IGraphMemoryService>();

        await Assert.ThrowsAsync<ArgumentException>(() => graphMemory.CreateNodeAsync(
            new CreateGraphNodeRequest(
                Guid.Empty,
                GraphSpace.Trusted,
                "Part",
                TrustState.Trusted,
                null,
                null),
            CancellationToken.None));
    }

    [Fact]
    public async Task NodesAreVisibleOnlyWithinTenant()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var graphMemory = serviceProvider.GetRequiredService<IGraphMemoryService>();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        var node = await graphMemory.CreateNodeAsync(
            new CreateGraphNodeRequest(
                tenantId,
                GraphSpace.Trusted,
                "Part",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["partNumber"] = "P-100" },
                new GraphSourceReference("erp", "P-100", "batch-a")),
            CancellationToken.None);

        var visible = await graphMemory.GetNodeAsync(tenantId, node.NodeId, CancellationToken.None);
        var hidden = await graphMemory.GetNodeAsync(otherTenantId, node.NodeId, CancellationToken.None);

        Assert.NotNull(visible);
        Assert.Null(hidden);
        Assert.Equal("P-100", visible.Attributes["partNumber"]);
    }

    [Fact]
    public async Task RelationshipMetadataIsPersistedAndTraversed()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var graphMemory = serviceProvider.GetRequiredService<IGraphMemoryService>();
        var tenantId = Guid.NewGuid();
        var parent = await CreateNodeAsync(graphMemory, tenantId, "Assembly");
        var child = await CreateNodeAsync(graphMemory, tenantId, "Part");

        var relationship = await graphMemory.CreateRelationshipAsync(
            new CreateGraphRelationshipRequest(
                tenantId,
                parent.NodeId,
                child.NodeId,
                "contains",
                TrustState.Provisional,
                new Dictionary<string, string?> { ["quantity"] = "2" },
                new GraphSourceReference("pdm", "bom-1", "batch-a")),
            CancellationToken.None);

        var traversal = await graphMemory.TraverseAsync(
            new TraverseGraphRequest(
                tenantId,
                parent.NodeId,
                GraphSpace.Trusted,
                2,
                ["contains"],
                [TrustState.Trusted, TrustState.Provisional]),
            CancellationToken.None);

        Assert.Equal("contains", relationship.RelationshipType);
        Assert.Equal("2", relationship.Attributes["quantity"]);
        Assert.Contains(traversal.Nodes, node => node.NodeId == child.NodeId);
        Assert.Contains(traversal.Relationships, traversed => traversed.RelationshipId == relationship.RelationshipId);
        Assert.All(traversal.Relationships, traversed => Assert.Equal(tenantId, traversed.TenantId));
    }

    [Fact]
    public async Task RelationshipTraversalRespectsTenantBoundary()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var graphMemory = serviceProvider.GetRequiredService<IGraphMemoryService>();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var parent = await CreateNodeAsync(graphMemory, tenantId, "Assembly");
        var child = await CreateNodeAsync(graphMemory, tenantId, "Part");
        var foreignChild = await CreateNodeAsync(graphMemory, otherTenantId, "Part");

        await graphMemory.CreateRelationshipAsync(
            new CreateGraphRelationshipRequest(tenantId, parent.NodeId, child.NodeId, "contains", TrustState.Trusted, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => graphMemory.CreateRelationshipAsync(
            new CreateGraphRelationshipRequest(tenantId, parent.NodeId, foreignChild.NodeId, "contains", TrustState.Trusted, null, null),
            CancellationToken.None));

        var traversal = await graphMemory.TraverseAsync(
            new TraverseGraphRequest(tenantId, parent.NodeId, GraphSpace.Trusted, 2, ["contains"], [TrustState.Trusted]),
            CancellationToken.None);

        Assert.Contains(traversal.Nodes, node => node.NodeId == child.NodeId);
        Assert.DoesNotContain(traversal.Nodes, node => node.NodeId == foreignChild.NodeId);
        Assert.All(traversal.Nodes, node => Assert.Equal(tenantId, node.TenantId));
    }

    [Fact]
    public async Task EnsureNodeAndRelationshipAreIdempotentByIdentityKeyAndEndpoints()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var graphMemory = serviceProvider.GetRequiredService<IGraphMemoryService>();
        var bootstrapService = serviceProvider.GetRequiredService<IGraphBootstrapService>();
        await bootstrapService.BootstrapAsync(CancellationToken.None);

        var tenantId = Guid.NewGuid();
        var identityKey = GraphIdentityKeyBuilder.Build(
            "SOLIDWORKS-PDM",
            "part",
            new Dictionary<string, string?> { ["documentId"] = "2" });
        Assert.NotNull(identityKey);

        var first = await graphMemory.EnsureNodeAsync(
            new CreateGraphNodeRequest(
                tenantId,
                GraphSpace.Staging,
                "part",
                TrustState.Unverified,
                new Dictionary<string, string?> { ["documentId"] = "2", ["fileName"] = "part2.sldprt" },
                new GraphSourceReference("SOLIDWORKS-PDM", "2", "batch-a"),
                identityKey),
            CancellationToken.None);
        var second = await graphMemory.EnsureNodeAsync(
            new CreateGraphNodeRequest(
                tenantId,
                GraphSpace.Staging,
                "part",
                TrustState.Unverified,
                new Dictionary<string, string?> { ["documentId"] = "2", ["fileName"] = "part2-updated.sldprt" },
                new GraphSourceReference("SOLIDWORKS-PDM", "2", "batch-b"),
                identityKey),
            CancellationToken.None);

        Assert.Equal(first.NodeId, second.NodeId);
        Assert.Equal("part2-updated.sldprt", second.Attributes["fileName"]);

        var childIdentityKey = GraphIdentityKeyBuilder.Build(
            "SOLIDWORKS-PDM",
            "partVersion",
            new Dictionary<string, string?> { ["pdmVersionKey"] = "2-1" });
        Assert.NotNull(childIdentityKey);
        var child = await graphMemory.EnsureNodeAsync(
            new CreateGraphNodeRequest(
                tenantId,
                GraphSpace.Staging,
                "partVersion",
                TrustState.Unverified,
                new Dictionary<string, string?> { ["pdmVersionKey"] = "2-1" },
                new GraphSourceReference("SOLIDWORKS-PDM", "2-1", "batch-a"),
                childIdentityKey),
            CancellationToken.None);

        var relationship = await graphMemory.EnsureRelationshipAsync(
            new CreateGraphRelationshipRequest(
                tenantId,
                first.NodeId,
                child.NodeId,
                "HAS_VERSION",
                TrustState.Unverified,
                null,
                new GraphSourceReference("SOLIDWORKS-PDM", "2|2-1", "batch-c")),
            CancellationToken.None);
        var duplicateRelationship = await graphMemory.EnsureRelationshipAsync(
            new CreateGraphRelationshipRequest(
                tenantId,
                first.NodeId,
                child.NodeId,
                "HAS_VERSION",
                TrustState.Unverified,
                new Dictionary<string, string?> { ["note"] = "linked" },
                new GraphSourceReference("SOLIDWORKS-PDM", "2|2-1", "batch-d")),
            CancellationToken.None);

        Assert.Equal(relationship.RelationshipId, duplicateRelationship.RelationshipId);
        Assert.Equal("linked", duplicateRelationship.Attributes["note"]);
    }

    [Fact]
    public async Task DomainAttributesAreStoredAsPrefixedFlatPropertiesOnNodesAndRelationships()
    {
        await using var serviceProvider = CreateGraphServiceProvider();
        var graphMemory = serviceProvider.GetRequiredService<IGraphMemoryService>();
        var driver = serviceProvider.GetRequiredService<IDriver>();
        var tenantId = Guid.NewGuid();
        var parent = await graphMemory.CreateNodeAsync(
            new CreateGraphNodeRequest(
                tenantId,
                GraphSpace.Staging,
                "partVersion",
                TrustState.Unverified,
                new Dictionary<string, string?>
                {
                    ["pdmVersionKey"] = "2-22",
                    ["status"] = "MFG",
                    ["revision"] = "22"
                },
                new GraphSourceReference("SOLIDWORKS-PDM", "2-22", "batch-a")),
            CancellationToken.None);
        var child = await CreateNodeAsync(graphMemory, tenantId, "Part");

        var relationship = await graphMemory.CreateRelationshipAsync(
            new CreateGraphRelationshipRequest(
                tenantId,
                parent.NodeId,
                child.NodeId,
                "HAS_VERSION",
                TrustState.Unverified,
                new Dictionary<string, string?> { ["quantity"] = "1" },
                new GraphSourceReference("SOLIDWORKS-PDM", "2|2-22", "batch-a")),
            CancellationToken.None);

        Assert.Equal("MFG", parent.Attributes["status"]);

        await using var session = driver.AsyncSession(builder => builder.WithDatabase("neo4j"));
        var nodeCursor = await session.RunAsync(
            """
            MATCH (node:BaseNode { tenantId: $tenantId, nodeId: $nodeId })
            RETURN node
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = tenantId.ToString(),
                ["nodeId"] = parent.NodeId.ToString()
            });
        var nodeRecord = await nodeCursor.SingleAsync();
        var nodeProperties = nodeRecord["node"].As<INode>().Properties;

        Assert.True(nodeProperties.ContainsKey("attributesJson"));
        Assert.Equal("MFG", Convert.ToString(nodeProperties["attr_status"]));
        Assert.Equal("2-22", Convert.ToString(nodeProperties["attr_pdmVersionKey"]));
        Assert.Equal("22", Convert.ToString(nodeProperties["attr_revision"]));

        var relationshipCursor = await session.RunAsync(
            """
            MATCH ()-[relationship:BASE_RELATIONSHIP { tenantId: $tenantId, relationshipId: $relationshipId }]->()
            RETURN relationship
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = tenantId.ToString(),
                ["relationshipId"] = relationship.RelationshipId.ToString()
            });
        var relationshipRecord = await relationshipCursor.SingleAsync();
        var relationshipProperties = relationshipRecord["relationship"].As<IRelationship>().Properties;

        Assert.True(relationshipProperties.ContainsKey("attributesJson"));
        Assert.Equal("1", Convert.ToString(relationshipProperties["attr_quantity"]));
    }

    [Fact]
    public void MemgraphAdapterRemainsDisabledByDefault()
    {
        using var serviceProvider = CreateGraphServiceProvider(useContainer: false);

        Assert.Empty(serviceProvider.GetServices<MemgraphGraphMemoryService>());
    }

    [Fact]
    public async Task NoPublicGraphQueryRoutesExist()
    {
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false"
                    });
                });
            });
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/graph/query", new { cypher = "MATCH (node) RETURN node" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<BaseNode> CreateNodeAsync(IGraphMemoryService graphMemory, Guid tenantId, string objectType)
    {
        return await graphMemory.CreateNodeAsync(
            new CreateGraphNodeRequest(
                tenantId,
                GraphSpace.Trusted,
                objectType,
                TrustState.Trusted,
                null,
                null),
            CancellationToken.None);
    }

    private ServiceProvider CreateGraphServiceProvider(bool useContainer = true, bool bootstrapOnStartup = false)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["GraphMemory:Provider"] = GraphMemoryProviderNames.Neo4j,
            ["GraphMemory:Neo4j:Uri"] = useContainer ? fixture.Container.GetConnectionString() : "bolt://localhost:7687",
            ["GraphMemory:Neo4j:Username"] = Neo4jUsername,
            ["GraphMemory:Neo4j:Password"] = Neo4jPassword,
            ["GraphMemory:Neo4j:Database"] = "neo4j",
            ["GraphMemory:Neo4j:Enabled"] = "true",
            ["GraphMemory:Neo4j:BootstrapOnStartup"] = bootstrapOnStartup.ToString(),
            ["GraphMemory:Memgraph:Enabled"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddEnterpriseThreadGraphMemory(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    public sealed class Neo4jFixture : IAsyncLifetime
    {
        public Neo4jContainer Container { get; } = new Neo4jBuilder("neo4j:5")
            .WithEnvironment("NEO4J_AUTH", $"{Neo4jUsername}/{Neo4jPassword}")
            .Build();

        public async Task InitializeAsync()
        {
            await Container.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Container.DisposeAsync();
        }
    }
}
