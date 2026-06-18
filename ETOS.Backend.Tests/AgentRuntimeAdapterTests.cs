using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class AgentRuntimeAdapterTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<PydanticAiRuntimeAdapter>();
        services.AddScoped<HermesRuntimeAdapter>();
        services.AddScoped<LangGraphRuntimeAdapter>();
        services.AddScoped<IAgentRuntimeAdapter, PydanticAiRuntimeAdapter>(sp => sp.GetRequiredService<PydanticAiRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapter, HermesRuntimeAdapter>(sp => sp.GetRequiredService<HermesRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapter, LangGraphRuntimeAdapter>(sp => sp.GetRequiredService<LangGraphRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapterSelector, AgentRuntimeAdapterSelector>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AllAdaptersRegisteredInDi()
    {
        using var provider = CreateProvider();

        var adapters = provider.GetServices<IAgentRuntimeAdapter>().ToList();

        Assert.Equal(3, adapters.Count);
        Assert.Contains(adapters, adapter => adapter.AdapterKey == AgentRuntimeAdapterKeys.PydanticAi);
        Assert.Contains(adapters, adapter => adapter.AdapterKey == AgentRuntimeAdapterKeys.Hermes);
        Assert.Contains(adapters, adapter => adapter.AdapterKey == AgentRuntimeAdapterKeys.LangGraph);
    }

    [Fact]
    public void SelectorResolvesByKey()
    {
        using var provider = CreateProvider();
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var pydantic = selector.Resolve(AgentRuntimeAdapterKeys.PydanticAi);
        var hermes = selector.Resolve(AgentRuntimeAdapterKeys.Hermes);
        var langGraph = selector.Resolve(AgentRuntimeAdapterKeys.LangGraph);

        Assert.Equal(AgentRuntimeAdapterKeys.PydanticAi, pydantic.AdapterKey);
        Assert.Equal(AgentRuntimeAdapterKeys.Hermes, hermes.AdapterKey);
        Assert.Equal(AgentRuntimeAdapterKeys.LangGraph, langGraph.AdapterKey);
    }

    [Fact]
    public async Task PydanticAiStubThrowsExpectedDisabledMessage()
    {
        using var provider = CreateProvider();
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            selector.ExecuteAsync(
                new AgentRuntimeExecutionRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "{}",
                    "{}",
                    true,
                    AgentRuntimeAdapterKeys.PydanticAi),
                CancellationToken.None));

        Assert.Contains("PydanticAI agent runtime is not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HermesStubThrowsDeferredMessage()
    {
        using var provider = CreateProvider();
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            selector.ExecuteAsync(
                new AgentRuntimeExecutionRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "{}",
                    "{}",
                    true,
                    AgentRuntimeAdapterKeys.Hermes),
                CancellationToken.None));

        Assert.Contains("Hermes agent runtime is deferred", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LangGraphStubThrowsDeferredMessage()
    {
        using var provider = CreateProvider();
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            selector.ExecuteAsync(
                new AgentRuntimeExecutionRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "{}",
                    "{}",
                    true,
                    AgentRuntimeAdapterKeys.LangGraph),
                CancellationToken.None));

        Assert.Contains("LangGraph agent runtime is deferred", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownAdapterKeyRejected()
    {
        using var provider = CreateProvider();
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var exception = Assert.Throws<RequestValidationException>(() => selector.Resolve("unknown-adapter-v1"));

        Assert.Contains("not registered", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
