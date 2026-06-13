using System.Text.Json;
using System.Text.Json.Nodes;

namespace ETOS.Backend.GovernedChat.Llm;

public sealed class DeterministicLlmCompletionService : ILlmCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "Deterministic";

    public Task<string> CompleteStructuredAsync(
        string prompt,
        string outputSchemaJson,
        CancellationToken cancellationToken)
    {
        var contextBlock = ExtractContextBlock(prompt);
        var visibleItems = ParseContextItems(contextBlock);
        var question = ExtractQuestion(prompt);

        var schemaRoot = JsonNode.Parse(outputSchemaJson) as JsonObject;
        var required = schemaRoot?["required"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(item => item.Length > 0).ToList()
            ?? ["answer", "evidence", "confidence"];

        var output = new JsonObject();
        if (required.Contains("answer"))
        {
            output["answer"] = BuildAnswer(question, visibleItems);
        }

        if (required.Contains("evidence"))
        {
            output["evidence"] = new JsonArray(visibleItems.Select(item => new JsonObject
            {
                ["contextId"] = item.ContextId,
                ["contextType"] = item.ContextType,
                ["safeSummary"] = item.SafeSummary
            }).ToArray());
        }

        if (required.Contains("confidence"))
        {
            output["confidence"] = new JsonObject
            {
                ["overall"] = visibleItems.Count > 0 ? 0.82 : 0.35,
                ["notes"] = visibleItems.Count > 0
                    ? "Deterministic provider synthesized answer from LLM-visible context only."
                    : "No LLM-visible context was available for this turn."
            };
        }

        if (required.Contains("intentKey"))
        {
            output["intentKey"] = ExtractIntentKey(prompt) ?? "chat-derived-intent";
        }

        if (required.Contains("name"))
        {
            output["name"] = $"Chat draft {DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        }

        if (required.Contains("summary"))
        {
            output["summary"] = $"Draft generated from governed chat question: {Trim(question, 200)}";
        }

        if (required.Contains("widgets"))
        {
            output["widgets"] = new JsonArray(new JsonObject
            {
                ["widgetId"] = "primary-context",
                ["title"] = "Governed context",
                ["kind"] = "governed_query",
                ["queryIntentRef"] = ExtractIntentKey(prompt) ?? "object-360-context",
                ["visualization"] = "summary_list"
            },
            new JsonObject
            {
                ["widgetId"] = "open-reviews-kpi",
                ["title"] = "Open Reviews",
                ["kind"] = "governance_kpi_placeholder",
                ["kpiKey"] = "open_reviews"
            });
        }

        if (required.Contains("sections"))
        {
            output["sections"] = new JsonArray(new JsonObject
            {
                ["sectionId"] = "evidence-summary",
                ["title"] = "Evidence summary",
                ["kind"] = "governed_query",
                ["queryIntentRef"] = ExtractIntentKey(prompt) ?? "object-360-context",
                ["visualization"] = "summary_list"
            });
        }

        if (required.Contains("defaultAnchor"))
        {
            output["defaultAnchor"] = new JsonObject
            {
                ["startGraphNodeId"] = null,
                ["documentArtifactId"] = null
            };
        }

        if (required.Contains("createdFromChat"))
        {
            output["createdFromChat"] = true;
        }

        if (required.Contains("title"))
        {
            output["title"] = $"Recommendation from chat {DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        }

        if (required.Contains("recommendationType"))
        {
            output["recommendationType"] = "bomSync";
        }

        if (required.Contains("riskState"))
        {
            output["riskState"] = "medium";
        }

        if (required.Contains("capabilityState"))
        {
            output["capabilityState"] = "readOnlyAnalysis";
        }

        if (required.Contains("evidenceLinks"))
        {
            output["evidenceLinks"] = new JsonArray(visibleItems.Select(item => new JsonObject
            {
                ["linkId"] = Guid.NewGuid().ToString(),
                ["evidenceType"] = "manualNote",
                ["sourceId"] = Guid.TryParse(item.ContextId, out var parsedId) ? parsedId.ToString() : Guid.NewGuid().ToString(),
                ["safeSummary"] = item.SafeSummary,
                ["trustState"] = "provisional",
                ["permissionFiltered"] = false
            }).ToArray());
        }

        if (required.Contains("suggestedActions"))
        {
            output["suggestedActions"] = new JsonArray(new JsonObject
            {
                ["actionId"] = Guid.NewGuid().ToString(),
                ["title"] = "Review governed chat recommendation",
                ["kind"] = "REVIEW_RECOMMENDATION",
                ["riskScore"] = "medium",
                ["requiredReviewPath"] = "ENGINEERING_REVIEW",
                ["status"] = "proposed"
            });
        }

        if (required.Contains("relatedObjects"))
        {
            output["relatedObjects"] = new JsonArray();
        }

        return Task.FromResult(output.ToJsonString(JsonOptions));
    }

    private static string BuildAnswer(string question, IReadOnlyCollection<ContextItem> visibleItems)
    {
        if (visibleItems.Count == 0)
        {
            return $"No governed context was available to answer: {Trim(question, 300)}";
        }

        var summaries = string.Join(" ", visibleItems.Take(5).Select(item => item.SafeSummary));
        return $"Based on {visibleItems.Count} governed context item(s), {Trim(summaries, 900)}";
    }

    private static string ExtractContextBlock(string prompt)
    {
        const string marker = "LLM-visible context:";
        var index = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? prompt[(index + marker.Length)..].Trim() : string.Empty;
    }

    private static string ExtractQuestion(string prompt)
    {
        const string marker = "User question:";
        var start = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return prompt;
        }

        start += marker.Length;
        var end = prompt.IndexOf("LLM-visible context:", start, StringComparison.OrdinalIgnoreCase);
        var slice = end >= 0 ? prompt[start..end] : prompt[start..];
        return slice.Trim();
    }

    private static string? ExtractIntentKey(string prompt)
    {
        const string marker = "Query intent:";
        var start = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = prompt.IndexOf('\n', start);
        return (end >= 0 ? prompt[start..end] : prompt[start..]).Trim();
    }

    private static IReadOnlyCollection<ContextItem> ParseContextItems(string contextBlock)
    {
        if (string.IsNullOrWhiteSpace(contextBlock))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyCollection<ContextItem>>(contextBlock, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed record ContextItem(string ContextId, string ContextType, string SafeSummary);
}
