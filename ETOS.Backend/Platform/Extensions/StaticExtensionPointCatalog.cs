namespace ETOS.Backend.Platform.Extensions;

public sealed class StaticExtensionPointCatalog : IExtensionPointCatalog
{
    private static readonly ExtensionPoint[] ExtensionPoints =
    [
        new("sql-server", "SQL Server operational store", "deferred", "Post-Issue 1", "Use EF Core provider abstraction when customer deployment requires SQL Server."),
        new("neo4j", "Neo4j graph backend", "active", "Issue 6", "Neo4j is the MVP graph memory backend behind the graph abstraction."),
        new("memgraph", "Memgraph graph backend", "optional", "Issue 6+", "Memgraph remains a disabled optional adapter placeholder unless explicitly enabled."),
        new("keycloak", "Keycloak identity provider", "deferred", "Issue 2+", "Identity starts in the modular monolith before enterprise IdP federation."),
        new("temporal", "Temporal workflow runtime", "deferred", "Workflow runtime slice", "Workflow contracts must be defined before runtime selection."),
        new("kubernetes", "Kubernetes deployment", "deferred", "Deployment hardening", "Docker Compose is the local foundation for Issue 1."),
        new("ci-cd", "CI/CD pipeline", "deferred", "Delivery automation slice", "Local verification commands are documented first.")
    ];

    public IReadOnlyCollection<ExtensionPoint> List()
    {
        return ExtensionPoints;
    }
}
