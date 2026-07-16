using System.Net;
using System.Text;
using System.Text.Json;
using ETOS.Backend.Documents.Vector;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class EmbeddingProviderTests
{
    [Fact]
    public void Factory_DefaultsToDeterministic()
    {
        var provider = EmbeddingProviderFactory.Create(
            new StubHttpClientFactory(),
            Options.Create(new DocumentVectorIndexingOptions
            {
                Embedding = new DocumentEmbeddingOptions
                {
                    Provider = "deterministic-v1",
                    Dimensions = 64
                }
            }));

        Assert.Equal(EmbeddingProviderKeys.Deterministic, provider.ProviderKey);
        Assert.IsType<DeterministicEmbeddingProvider>(provider);
    }

    [Fact]
    public void Factory_FallsBackToDeterministic_WhenOpenAiMissingApiKey()
    {
        var previousKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var previousBase = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", null);

            var provider = EmbeddingProviderFactory.Create(
                new StubHttpClientFactory(),
                Options.Create(new DocumentVectorIndexingOptions
                {
                    Embedding = new DocumentEmbeddingOptions
                    {
                        Provider = "openai",
                        ApiKey = null,
                        BaseUrl = null,
                        Dimensions = 64
                    }
                }));

            Assert.IsType<DeterministicEmbeddingProvider>(provider);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousKey);
            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", previousBase);
        }
    }

    [Fact]
    public void Factory_UsesOpenAiCompatible_WhenBaseUrlConfigured()
    {
        var previousKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var previousBase = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", null);

            var provider = EmbeddingProviderFactory.Create(
                new StubHttpClientFactory(),
                Options.Create(new DocumentVectorIndexingOptions
                {
                    Embedding = new DocumentEmbeddingOptions
                    {
                        Provider = "openai-compatible",
                        BaseUrl = "http://localhost:1234/v1",
                        Dimensions = 8
                    }
                }));

            Assert.Equal(EmbeddingProviderKeys.OpenAiCompatible, provider.ProviderKey);
            Assert.IsType<OpenAiCompatibleEmbeddingProvider>(provider);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousKey);
            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", previousBase);
        }
    }

    [Fact]
    public async Task OpenAiCompatible_ParsesEmbeddingResponse()
    {
        var handler = new StubEmbeddingHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://localhost:1234/v1/embeddings", request.RequestUri!.ToString());
            Assert.Equal("Bearer lm-studio", request.Headers.Authorization!.ToString());

            var vector = Enumerable.Range(0, 8).Select(i => (float)i / 10f).ToArray();
            return CreateEmbeddingResponse(vector);
        });

        var httpClient = new HttpClient(handler);
        var provider = new OpenAiCompatibleEmbeddingProvider(
            httpClient,
            new DocumentEmbeddingOptions
            {
                Provider = EmbeddingProviderKeys.OpenAiCompatible,
                Model = "nomic-embed-text",
                Dimensions = 8
            },
            EmbeddingProviderKeys.OpenAiCompatible,
            "http://localhost:1234/v1",
            OpenAiCompatibleEmbeddingProvider.DefaultCompatibleApiKey);

        var embedding = await provider.EmbedAsync("brake pad wear", CancellationToken.None);

        Assert.Equal(8, embedding.Count);
        Assert.Equal(0.0f, embedding[0]);
        Assert.Equal(0.7f, embedding[7]);
    }

    [Fact]
    public async Task OpenAiCompatible_Throws_WhenDimensionMismatch()
    {
        var handler = new StubEmbeddingHttpHandler(_ => CreateEmbeddingResponse([0.1f, 0.2f, 0.3f]));
        var provider = new OpenAiCompatibleEmbeddingProvider(
            new HttpClient(handler),
            new DocumentEmbeddingOptions { Dimensions = 8 },
            EmbeddingProviderKeys.OpenAi,
            OpenAiCompatibleEmbeddingProvider.DefaultCloudBaseUrl,
            "test-key");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EmbedAsync("mismatch", CancellationToken.None));

        Assert.Contains("dimensions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deterministic_IsStableForSameInput()
    {
        var provider = new DeterministicEmbeddingProvider(16);
        var first = await provider.EmbedAsync("same text", CancellationToken.None);
        var second = await provider.EmbedAsync("same text", CancellationToken.None);

        Assert.Equal(first, second);
    }

    private static HttpResponseMessage CreateEmbeddingResponse(float[] vector)
    {
        var payload = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { embedding = vector }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubEmbeddingHttpHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called for this test.")));
    }

    private sealed class StubEmbeddingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
