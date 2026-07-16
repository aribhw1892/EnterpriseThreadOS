using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Documents.Vector;

/// <summary>
/// OpenAI Embeddings API client used for both cloud (<c>openai</c>) and local OpenAI-compatible hosts.
/// </summary>
public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    public const string DefaultCloudBaseUrl = "https://api.openai.com/v1";
    public const string DefaultCompatibleApiKey = "lm-studio";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DocumentEmbeddingOptions _options;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public OpenAiCompatibleEmbeddingProvider(
        HttpClient httpClient,
        DocumentEmbeddingOptions options,
        string providerKey,
        string baseUrl,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Embedding dimensions must be positive.");
        }

        _httpClient = httpClient;
        _options = options;
        ProviderKey = EmbeddingProviderKeys.Normalize(providerKey);
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
    }

    public string ProviderKey { get; }

    public int Dimensions => _options.Dimensions;

    public async Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "text-embedding-3-small"
            : _options.Model.Trim();

        object requestBody = IsCloudProvider(ProviderKey)
            ? new
            {
                model,
                input = text ?? string.Empty,
                dimensions = _options.Dimensions
            }
            : new
            {
                model,
                input = text ?? string.Empty
            };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Embedding provider '{ProviderKey}' failed with status {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || data.GetArrayLength() == 0
            || !data[0].TryGetProperty("embedding", out var embeddingElement))
        {
            throw new InvalidOperationException(
                $"Embedding provider '{ProviderKey}' returned an unexpected payload.");
        }

        var vector = new float[embeddingElement.GetArrayLength()];
        var index = 0;
        foreach (var value in embeddingElement.EnumerateArray())
        {
            vector[index++] = value.GetSingle();
        }

        if (vector.Length != Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding provider '{ProviderKey}' returned {vector.Length} dimensions but DocumentVectorIndexing:Embedding:Dimensions is {Dimensions}. Recreate the Qdrant collection after changing dimensions.");
        }

        return vector;
    }

    private static bool IsCloudProvider(string providerKey)
    {
        var normalized = EmbeddingProviderKeys.Normalize(providerKey);
        return normalized is EmbeddingProviderKeys.OpenAi or EmbeddingProviderKeys.OpenAiV1;
    }
}

public static class EmbeddingProviderFactory
{
    public static IEmbeddingProvider Create(
        IHttpClientFactory httpClientFactory,
        IOptions<DocumentVectorIndexingOptions> optionsAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(optionsAccessor);

        var options = optionsAccessor.Value.Embedding ?? new DocumentEmbeddingOptions();
        var providerKey = EmbeddingProviderKeys.Normalize(options.Provider);

        if (!EmbeddingProviderKeys.IsOpenAiFamily(providerKey))
        {
            return new DeterministicEmbeddingProvider(options.Dimensions);
        }

        var baseUrl = ResolveBaseUrl(providerKey, options);
        var apiKey = ResolveApiKey(providerKey, options);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new DeterministicEmbeddingProvider(options.Dimensions);
        }

        if (IsCloudProvider(providerKey) && string.IsNullOrWhiteSpace(apiKey))
        {
            return new DeterministicEmbeddingProvider(options.Dimensions);
        }

        var httpClient = httpClientFactory.CreateClient(nameof(OpenAiCompatibleEmbeddingProvider));
        return new OpenAiCompatibleEmbeddingProvider(
            httpClient,
            options,
            providerKey,
            baseUrl,
            string.IsNullOrWhiteSpace(apiKey) ? OpenAiCompatibleEmbeddingProvider.DefaultCompatibleApiKey : apiKey);
    }

    public static string? ResolveBaseUrl(string providerKey, DocumentEmbeddingOptions options)
    {
        if (IsCloudProvider(providerKey))
        {
            // Explicit config only — do not reuse OPENAI_BASE_URL (that is for openai-compatible / LM Studio).
            return string.IsNullOrWhiteSpace(options.BaseUrl)
                ? OpenAiCompatibleEmbeddingProvider.DefaultCloudBaseUrl
                : options.BaseUrl.Trim().TrimEnd('/');
        }

        return FirstNonEmpty(options.BaseUrl, Environment.GetEnvironmentVariable("OPENAI_BASE_URL"))?.TrimEnd('/');
    }

    public static string? ResolveApiKey(string providerKey, DocumentEmbeddingOptions options)
    {
        return FirstNonEmpty(options.ApiKey, Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    private static bool IsCloudProvider(string providerKey)
    {
        var normalized = EmbeddingProviderKeys.Normalize(providerKey);
        return normalized is EmbeddingProviderKeys.OpenAi or EmbeddingProviderKeys.OpenAiV1;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
