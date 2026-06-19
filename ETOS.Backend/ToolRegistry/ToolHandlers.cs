using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public interface IToolHandler
{
    string HandlerKey { get; }

    Task<ToolHandlerResult> ExecuteAsync(ToolHandlerContext context, CancellationToken cancellationToken);

    ToolHandlerDryRunResult SimulateDryRun(ToolHandlerContext context);
}

public sealed record ToolHandlerContext(
    Guid TenantId,
    Guid UserId,
    string InputJson,
    ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument ToolDocument,
    ConnectorDefinitionPayloadParser.ConnectorDefinitionPayloadDocument? ConnectorDocument);

public sealed record ToolHandlerResult(
    bool Succeeded,
    string OutputSafeSummaryJson,
    string? ErrorSafeSummary,
    Guid? RetrievalRunId);

public sealed record ToolHandlerDryRunResult(
    string ExpectedOutputSchemaJson,
    string SimulationSafeSummary,
    string? ConnectorCredentialSafeSummaryJson);

public sealed class GovernedQueryToolHandler(IGovernedQueryService governedQueryService) : IToolHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string HandlerKey => ToolInternalHandlerKeys.GovernedQuery;

    public async Task<ToolHandlerResult> ExecuteAsync(ToolHandlerContext context, CancellationToken cancellationToken)
    {
        var input = ParseInput(context.InputJson, context.ToolDocument);
        var run = await governedQueryService.RunAsync(
            new RunGovernedQueryRequest(
                input.IntentKey,
                input.StartGraphNodeId,
                input.DocumentArtifactId,
                input.PolicyKey,
                input.QueryText,
                input.MaxDepth),
            cancellationToken);

        var output = new
        {
            retrievalRunId = run.Id,
            safeSummary = run.SafeSummary,
            retrievedCount = run.RetrievedCount,
            filteredCount = run.FilteredCount,
            deniedCount = run.DeniedCount,
            status = run.Status
        };

        return new ToolHandlerResult(
            true,
            JsonSerializer.Serialize(output, JsonOptions),
            null,
            run.Id);
    }

    public ToolHandlerDryRunResult SimulateDryRun(ToolHandlerContext context)
    {
        var input = ParseInput(context.InputJson, context.ToolDocument);
        return new ToolHandlerDryRunResult(
            context.ToolDocument.OutputSchemaJson ?? "{}",
            $"Dry-run would execute governed query intent '{input.IntentKey}' with policy-filtered context assembly. No retrieval run was created.",
            null);
    }

    private static GovernedQueryToolInput ParseInput(
        string inputJson,
        ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument toolDocument)
    {
        var node = JsonNode.Parse(inputJson) as JsonObject
            ?? throw new RequestValidationException("Tool input must be a JSON object.");

        var intentKey = node["intentKey"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(intentKey))
        {
            throw new RequestValidationException("intentKey is required.");
        }

        if (toolDocument.AllowedQueryIntentKeys is { Count: > 0 }
            && !toolDocument.AllowedQueryIntentKeys.Contains(intentKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"intentKey '{intentKey}' is not allowed for this tool.");
        }

        return new GovernedQueryToolInput(
            intentKey,
            node["queryText"]?.GetValue<string>()?.Trim() ?? intentKey,
            ParseOptionalGuid(node["startGraphNodeId"]),
            ParseOptionalGuid(node["documentArtifactId"]),
            node["policyKey"]?.GetValue<string>()?.Trim(),
            node["maxDepth"]?.GetValue<int>() ?? 2);
    }

    private static Guid? ParseOptionalGuid(JsonNode? node)
    {
        var value = node?.GetValue<string>()?.Trim();
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed record GovernedQueryToolInput(
        string IntentKey,
        string QueryText,
        Guid? StartGraphNodeId,
        Guid? DocumentArtifactId,
        string? PolicyKey,
        int MaxDepth);
}

public sealed class DisabledWriteConnectorToolHandler : IToolHandler
{
    public string HandlerKey => ToolInternalHandlerKeys.DisabledWriteConnector;

    public Task<ToolHandlerResult> ExecuteAsync(ToolHandlerContext context, CancellationToken cancellationToken)
        => Task.FromResult(new ToolHandlerResult(
            false,
            "{}",
            "Write-capable connector execution is disabled in MVP.",
            null));

    public ToolHandlerDryRunResult SimulateDryRun(ToolHandlerContext context)
        => new(
            context.ToolDocument.OutputSchemaJson ?? "{}",
            "Dry-run confirms write-capable connector contract remains disabled in MVP.",
            null);
}
