using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Ontology;

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

public sealed class MappingPredictorToolHandler(IModelPackageContextResolver modelPackageContextResolver) : IToolHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string HandlerKey => ToolInternalHandlerKeys.MappingPredictor;

    public async Task<ToolHandlerResult> ExecuteAsync(ToolHandlerContext context, CancellationToken cancellationToken)
    {
        var input = ParseInput(context.InputJson);
        var tenantContext = new ActiveTenantContext(
            context.TenantId,
            "mapping-predictor",
            "mapping-predictor",
            context.UserId);
        var modelContext = await modelPackageContextResolver.ResolvePublishedAsync(
            input.ModelPackageVersionId,
            tenantContext,
            "tools.execute",
            cancellationToken);

        var columnSuggestions = RuleBasedMappingProvider
            .BuildColumnSuggestions(input.Headers, modelContext)
            .ToList();
        var lifecycleSuggestions = RuleBasedMappingProvider
            .BuildLifecycleSuggestions(input.Headers, input.SampleRows, modelContext, columnSuggestions)
            .ToList();

        var output = new
        {
            providerKey = MappingSuggestionProviderKeys.RuleBased,
            columnSuggestionCount = columnSuggestions.Count,
            lifecycleSuggestionCount = lifecycleSuggestions.Count,
            columnSuggestions = columnSuggestions
                .Take(8)
                .Select(item => new
                {
                    item.SourceColumn,
                    item.CanonicalObjectType,
                    item.CanonicalAttributeKey,
                    item.Confidence,
                    item.Rationale
                }),
            lifecycleSuggestions = lifecycleSuggestions
                .Take(8)
                .Select(item => new
                {
                    item.SourceValue,
                    item.CanonicalLifecycleKey,
                    item.Confidence,
                    item.Rationale
                })
        };

        return new ToolHandlerResult(
            true,
            JsonSerializer.Serialize(output, JsonOptions),
            null,
            null);
    }

    public ToolHandlerDryRunResult SimulateDryRun(ToolHandlerContext context)
    {
        var input = ParseInput(context.InputJson);
        return new ToolHandlerDryRunResult(
            context.ToolDocument.OutputSchemaJson ?? "{}",
            $"Dry-run would predict mapping suggestions for {input.Headers.Count} header(s) using rule-based heuristics.",
            null);
    }

    private static MappingPredictorInput ParseInput(string inputJson)
    {
        var node = JsonNode.Parse(inputJson) as JsonObject
            ?? throw new RequestValidationException("Tool input must be a JSON object.");

        var modelPackageVersionId = ParseRequiredGuid(node["modelPackageVersionId"], "modelPackageVersionId");
        var headers = node["headers"]?.AsArray()
            ?? throw new RequestValidationException("headers is required.");
        var sampleRowsNode = node["sampleRows"]?.AsArray() ?? [];

        var parsedHeaders = headers
            .Select(item => item?.GetValue<string>()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();

        var sampleRows = sampleRowsNode
            .Select(ParseSampleRow)
            .ToList();

        return new MappingPredictorInput(modelPackageVersionId, parsedHeaders, sampleRows);
    }

    private static Guid ParseRequiredGuid(JsonNode? node, string fieldName)
    {
        var value = node?.GetValue<string>()?.Trim();
        if (Guid.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new RequestValidationException($"{fieldName} is required.");
    }

    private static IReadOnlyDictionary<string, string?> ParseSampleRow(JsonNode? node)
    {
        if (node is not JsonObject rowObject)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        return rowObject.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    private sealed record MappingPredictorInput(
        Guid ModelPackageVersionId,
        IReadOnlyCollection<string> Headers,
        IReadOnlyCollection<IReadOnlyDictionary<string, string?>> SampleRows);
}
