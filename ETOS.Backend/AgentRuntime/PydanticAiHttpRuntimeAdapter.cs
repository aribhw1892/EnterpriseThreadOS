using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.AgentRuntime;

public sealed class PydanticAiRuntimeAdapter(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IAgentRuntimeAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string AdapterKey => AgentRuntimeAdapterKeys.PydanticAi;

    public async Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var runtimeOptions = options.Value;
        if (string.IsNullOrWhiteSpace(runtimeOptions.BaseUrl))
        {
            throw new RequestValidationException(
                "PydanticAI agent runtime is not configured for this deployment. Set AgentRuntime:BaseUrl.");
        }

        var promptTemplateBody = ExtractPromptTemplateBody(request.PromptTemplatePayloadJson);
        var fallbackModels = DeserializeFallbackModels(request.FallbackModelsJson);
        var payload = new ExecuteHttpRequest(
            request.GovernedContextSummaryJson ?? "{}",
            promptTemplateBody,
            request.OutputSchemaJson ?? "{}",
            request.PrimaryModelProviderKey ?? "deterministic",
            request.PrimaryModelId ?? "mock-v1",
            fallbackModels,
            request.StructuredInputJson,
            request.PreviewMode,
            request.ToolOutputSummariesJson);

        var baseUrl = runtimeOptions.BaseUrl.TrimEnd('/');
        using var response = await httpClient.PostAsJsonAsync($"{baseUrl}/v1/execute", payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ExecuteHttpResponse>(JsonOptions, cancellationToken)
            ?? throw new RequestValidationException("Agent runtime returned an empty response.");

        if (!response.IsSuccessStatusCode && !string.Equals(body.Status, AgentRuntimeExecutionStatuses.Succeeded, StringComparison.OrdinalIgnoreCase))
        {
            var detail = body.TraceNotes.Count > 0
                ? string.Join(" ", body.TraceNotes)
                : $"Agent runtime request failed with status {(int)response.StatusCode}.";
            throw new RequestValidationException(detail);
        }

        var fallbackAppliedJson = body.FallbackApplied
            ? JsonSerializer.Serialize(new { applied = true, modelUsed = body.ModelUsed }, JsonOptions)
            : null;

        return new AgentRuntimeExecutionResult(
            AdapterKey,
            body.Status,
            body.StructuredOutputJson,
            body.TraceNotes,
            body.ModelUsed,
            fallbackAppliedJson);
    }

    private static string ExtractPromptTemplateBody(string? promptTemplatePayloadJson)
    {
        if (string.IsNullOrWhiteSpace(promptTemplatePayloadJson))
        {
            return "Execute the governed agent task using only the provided context and structured input.";
        }

        try
        {
            using var document = JsonDocument.Parse(promptTemplatePayloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                return document.RootElement.GetString() ?? promptTemplatePayloadJson;
            }

            foreach (var propertyName in new[] { "template", "templateBody", "promptTemplateBody", "body" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return promptTemplatePayloadJson;
        }

        return promptTemplatePayloadJson;
    }

    private static IReadOnlyCollection<FallbackModelHttpRequest> DeserializeFallbackModels(string? fallbackModelsJson)
    {
        if (string.IsNullOrWhiteSpace(fallbackModelsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<FallbackModelHttpRequest>>(fallbackModelsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record ExecuteHttpRequest(
        [property: JsonPropertyName("governedContextSummaryJson")] string GovernedContextSummaryJson,
        [property: JsonPropertyName("promptTemplateBody")] string PromptTemplateBody,
        [property: JsonPropertyName("outputSchemaJson")] string OutputSchemaJson,
        [property: JsonPropertyName("primaryModelProviderKey")] string PrimaryModelProviderKey,
        [property: JsonPropertyName("primaryModelId")] string PrimaryModelId,
        [property: JsonPropertyName("fallbackModels")] IReadOnlyCollection<FallbackModelHttpRequest> FallbackModels,
        [property: JsonPropertyName("structuredInputJson")] string? StructuredInputJson,
        [property: JsonPropertyName("preview")] bool Preview,
        [property: JsonPropertyName("toolOutputSummariesJson")] string? ToolOutputSummariesJson);

    private sealed record FallbackModelHttpRequest(
        [property: JsonPropertyName("providerKey")] string ProviderKey,
        [property: JsonPropertyName("modelId")] string ModelId,
        [property: JsonPropertyName("triggerReason")] string? TriggerReason);

    private sealed record ExecuteHttpResponse(
        string Status,
        [property: JsonPropertyName("structuredOutputJson")] string? StructuredOutputJson,
        [property: JsonPropertyName("traceNotes")] IReadOnlyCollection<string> TraceNotes,
        [property: JsonPropertyName("modelUsed")] string? ModelUsed,
        [property: JsonPropertyName("fallbackApplied")] bool FallbackApplied);
}
