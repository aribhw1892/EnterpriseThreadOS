using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Identity;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class AgentRuntimeAdapterTests
{
    [Fact]
    public void AllAdaptersRegisteredInDi()
    {
        using var provider = CreateProvider(includeRuntimeUrl: true);

        var adapters = provider.GetServices<IAgentRuntimeAdapter>().ToList();

        Assert.Equal(3, adapters.Count);
        Assert.Contains(adapters, adapter => adapter.AdapterKey == AgentRuntimeAdapterKeys.PydanticAi);
        Assert.Contains(adapters, adapter => adapter.AdapterKey == AgentRuntimeAdapterKeys.Hermes);
        Assert.Contains(adapters, adapter => adapter.AdapterKey == AgentRuntimeAdapterKeys.LangGraph);
    }

    [Fact]
    public void SelectorResolvesByKey()
    {
        using var provider = CreateProvider(includeRuntimeUrl: true);
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var pydantic = selector.Resolve(AgentRuntimeAdapterKeys.PydanticAi);
        var hermes = selector.Resolve(AgentRuntimeAdapterKeys.Hermes);
        var langGraph = selector.Resolve(AgentRuntimeAdapterKeys.LangGraph);

        Assert.Equal(AgentRuntimeAdapterKeys.PydanticAi, pydantic.AdapterKey);
        Assert.Equal(AgentRuntimeAdapterKeys.Hermes, hermes.AdapterKey);
        Assert.Equal(AgentRuntimeAdapterKeys.LangGraph, langGraph.AdapterKey);
    }

    [Fact]
    public async Task PydanticAiHttpAdapterReturnsStructuredOutput()
    {
        var handler = MockAgentRuntimeHttpHandler.CreateSuccessHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://agent-runtime.test/")
        };
        var adapter = new PydanticAiRuntimeAdapter(
            httpClient,
            Options.Create(new AgentRuntimeOptions
            {
                BaseUrl = "http://agent-runtime.test",
                TimeoutSeconds = 30
            }));

        var result = await adapter.ExecuteAsync(
            new AgentRuntimeExecutionRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "{}",
                """{"queryText":"Adapter test"}""",
                PreviewMode: true,
                AgentRuntimeAdapterKeys.PydanticAi,
                AgentVersionId: Guid.NewGuid(),
                AgentRunId: Guid.NewGuid(),
                PromptTemplatePayloadJson: """{"template":"Analyze governed context."}""",
                OutputSchemaJson: """{"type":"object","required":["answer"],"properties":{"answer":{"type":"string"}}}""",
                PrimaryModelProviderKey: "deterministic",
                PrimaryModelId: "mock-v1"),
            CancellationToken.None);

        Assert.Equal(AgentRuntimeAdapterKeys.PydanticAi, result.AdapterKey);
        Assert.Equal(AgentRuntimeExecutionStatuses.Succeeded, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.StructuredOutputJson));
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("/v1/execute", handler.LastRequest.RequestUri?.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PydanticAiRequiresConfiguredBaseUrl()
    {
        var handler = new MockAgentRuntimeHttpHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://agent-runtime.test/")
        };
        var adapter = new PydanticAiRuntimeAdapter(
            httpClient,
            Options.Create(new AgentRuntimeOptions()));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            adapter.ExecuteAsync(
                new AgentRuntimeExecutionRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "{}",
                    "{}",
                    true,
                    AgentRuntimeAdapterKeys.PydanticAi),
                CancellationToken.None));

        Assert.Contains("AgentRuntime:BaseUrl", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HermesStubThrowsDeferredMessage()
    {
        using var provider = CreateProvider(includeRuntimeUrl: true);
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
        using var provider = CreateProvider(includeRuntimeUrl: true);
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
        using var provider = CreateProvider(includeRuntimeUrl: true);
        var selector = provider.GetRequiredService<IAgentRuntimeAdapterSelector>();

        var exception = Assert.Throws<RequestValidationException>(() => selector.Resolve("unknown-adapter-v1"));

        Assert.Contains("not registered", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateProvider(bool includeRuntimeUrl)
    {
        var services = new ServiceCollection();
        services.AddOptions<AgentRuntimeOptions>().Configure(options =>
        {
            options.BaseUrl = includeRuntimeUrl ? "http://agent-runtime.test" : null;
            options.TimeoutSeconds = 30;
        });
        services.AddHttpClient<PydanticAiRuntimeAdapter>();
        services.AddScoped<PydanticAiRuntimeAdapter>();
        services.AddScoped<HermesRuntimeAdapter>();
        services.AddScoped<LangGraphRuntimeAdapter>();
        services.AddScoped<IAgentRuntimeAdapter, PydanticAiRuntimeAdapter>(sp => sp.GetRequiredService<PydanticAiRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapter, HermesRuntimeAdapter>(sp => sp.GetRequiredService<HermesRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapter, LangGraphRuntimeAdapter>(sp => sp.GetRequiredService<LangGraphRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapterSelector, AgentRuntimeAdapterSelector>();
        return services.BuildServiceProvider();
    }
}
