using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ETOS.Backend.GraphMemory;

public static class GraphMemoryServiceExtensions
{
    public static IServiceCollection AddEnterpriseThreadGraphMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GraphMemoryOptions>()
            .Bind(configuration.GetSection(GraphMemoryOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => IsSupportedProvider(options.Provider),
                "Graph memory provider must be Neo4j or Memgraph.")
            .Validate(
                options => !IsMemgraphProvider(options.Provider) || options.Memgraph.Enabled,
                "Memgraph graph memory must be explicitly enabled before it can be selected.")
            .ValidateOnStart();

        var configuredOptions = configuration.GetSection(GraphMemoryOptions.SectionName).Get<GraphMemoryOptions>() ?? new GraphMemoryOptions();

        services.AddSingleton<Neo4jGraphDriverFactory>();
        services.AddSingleton<IDriver>(serviceProvider =>
            serviceProvider.GetRequiredService<Neo4jGraphDriverFactory>().CreateDriver());
        services.AddSingleton<Neo4jGraphBootstrapService>();
        services.AddSingleton<Neo4jGraphHealthService>();
        services.AddSingleton<Neo4jGraphMemoryService>();
        if (configuredOptions.Memgraph.Enabled)
        {
            services.AddSingleton<MemgraphGraphMemoryService>();
        }

        services.AddSingleton<IGraphMemoryService>(ResolveGraphMemoryService);
        services.AddSingleton<IGraphHealthService>(ResolveGraphHealthService);
        services.AddSingleton<IGraphBootstrapService>(ResolveGraphBootstrapService);
        services.AddSingleton<IGraphSnapshotService, DeferredGraphSnapshotService>();
        services.AddSingleton<IGraphDiffService, DeferredGraphDiffService>();

        if (IsNeo4jProvider(configuredOptions.Provider) &&
            configuredOptions.Neo4j.Enabled &&
            configuredOptions.Neo4j.BootstrapOnStartup)
        {
            services.AddHostedService<GraphBootstrapHostedService>();
        }

        return services;
    }

    private static IGraphMemoryService ResolveGraphMemoryService(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<GraphMemoryOptions>>().Value;

        return IsMemgraphProvider(options.Provider)
            ? serviceProvider.GetRequiredService<MemgraphGraphMemoryService>()
            : serviceProvider.GetRequiredService<Neo4jGraphMemoryService>();
    }

    private static IGraphHealthService ResolveGraphHealthService(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<GraphMemoryOptions>>().Value;

        return IsMemgraphProvider(options.Provider)
            ? serviceProvider.GetRequiredService<MemgraphGraphMemoryService>()
            : serviceProvider.GetRequiredService<Neo4jGraphHealthService>();
    }

    private static IGraphBootstrapService ResolveGraphBootstrapService(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<GraphMemoryOptions>>().Value;

        return IsMemgraphProvider(options.Provider)
            ? serviceProvider.GetRequiredService<MemgraphGraphMemoryService>()
            : serviceProvider.GetRequiredService<Neo4jGraphBootstrapService>();
    }

    private static bool IsSupportedProvider(string provider)
    {
        return IsNeo4jProvider(provider) || IsMemgraphProvider(provider);
    }

    private static bool IsNeo4jProvider(string provider)
    {
        return string.Equals(provider, GraphMemoryProviderNames.Neo4j, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMemgraphProvider(string provider)
    {
        return string.Equals(provider, GraphMemoryProviderNames.Memgraph, StringComparison.OrdinalIgnoreCase);
    }
}
