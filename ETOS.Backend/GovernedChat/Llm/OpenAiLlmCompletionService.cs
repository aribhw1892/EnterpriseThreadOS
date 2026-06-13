using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.GovernedChat.Llm;

public sealed class OpenAiLlmCompletionService(
    HttpClient httpClient,
    IOptions<GovernedChatLlmOptions> options) : ILlmCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "OpenAI";

    public async Task<string> CompleteStructuredAsync(
        string prompt,
        string outputSchemaJson,
        CancellationToken cancellationToken)
    {
        var llmOptions = options.Value;
        if (string.IsNullOrWhiteSpace(llmOptions.OpenAiApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured for governed chat.");
        }

        var requestBody = new
        {
            model = llmOptions.OpenAiModel,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "governed_chat_output",
                    strict = true,
                    schema = JsonSerializer.Deserialize<object>(outputSchemaJson, JsonOptions)
                }
            },
            messages = new object[]
            {
                new { role = "system", content = "Respond with JSON that matches the provided schema. Use only facts from the user prompt context block." },
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", llmOptions.OpenAiApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI completion failed with status {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(payload);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI completion returned empty content.");
        }

        return content;
    }
}
