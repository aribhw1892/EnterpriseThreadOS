using System.Text.Json;
using System.Text.Json.Serialization;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Identity;
using ETOS.Backend.ToolRegistry;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Imports.MappingSuggestions;

public sealed class PydanticAiMappingProvider(
    IAgentRuntimeAdapterSelector adapterSelector,
    IOptions<MappingSuggestionOptions> mappingOptions,
    IOptions<AgentRuntimeOptions> runtimeOptions,
    ITenantContextResolver tenantContextResolver,
    RuleBasedMappingProvider ruleBasedMappingProvider,
    IToolGateway toolGateway,
    IPublishedToolVersionResolver publishedToolVersionResolver) : IMappingSuggestionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderKey => MappingSuggestionProviderKeys.PydanticAi;

    public async Task<ImportMappingSuggestionResult> SuggestAsync(
        ImportMappingSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var options = mappingOptions.Value;
        var includeDiagnostics = request.IncludeDiagnostics;
        var diagnosticsBuilder = includeDiagnostics
            ? new MappingSuggestionDiagnosticsBuilder(ProviderKey, options)
            : null;

        if (!options.Enabled)
        {
            throw new RequestValidationException(
                "PydanticAI mapping suggestions are not enabled for this deployment. Set ImportMappingSuggestions:Enabled.");
        }

        if (string.IsNullOrWhiteSpace(runtimeOptions.Value.BaseUrl))
        {
            throw new RequestValidationException(
                "PydanticAI mapping suggestions require AgentRuntime:BaseUrl to be configured.");
        }

        var context = await tenantContextResolver.ResolveAsync("imports.mapping.preview", cancellationToken);
        var governedContextJson = MappingSuggestionContextBuilder.BuildGovernedContextJson(request.ModelContext);
        var structuredInputJson = MappingSuggestionContextBuilder.BuildStructuredInputJson(request);
        diagnosticsBuilder?.SetContextPayloads(governedContextJson, structuredInputJson);

        var prefetch = await PrefetchToolOutputsAsync(context, request, options, cancellationToken);
        diagnosticsBuilder?.SetPrefetch(prefetch);
        var fallbackModelsJson = JsonSerializer.Serialize(options.FallbackModels ?? [], JsonOptions);

        var runtimeRequest = new AgentRuntimeExecutionRequest(
            context.TenantId,
            context.UserId,
            AgentTemplateVersionId: null,
            governedContextJson,
            structuredInputJson,
            PreviewMode: true,
            options.RuntimeAdapterKey,
            AgentVersionId: null,
            AgentRunId: null,
            options.PromptTemplateBody,
            MappingSuggestionOutputSchema.Json,
            options.PrimaryModelProviderKey,
            options.PrimaryModelId,
            fallbackModelsJson,
            prefetch.ToolOutputSummariesJson);

        diagnosticsBuilder?.SetRuntimeRequest(runtimeRequest, options);

        AgentRuntimeExecutionResult runtimeResult;
        try
        {
            runtimeResult = await adapterSelector.ExecuteAsync(runtimeRequest, cancellationToken);
        }
        catch (Exception exception) when (options.FallbackToRuleBasedOnRuntimeFailure)
        {
            diagnosticsBuilder?.SetRuntimeFailure(exception.Message, usedRuleBasedFallback: true);
            var fallback = await ruleBasedMappingProvider.SuggestAsync(request, cancellationToken);
            return fallback with
            {
                ProviderKey = ProviderKey,
                Diagnostics = diagnosticsBuilder?.Build()
            };
        }

        diagnosticsBuilder?.SetRuntimeResult(runtimeResult);

        if (!string.Equals(runtimeResult.Status, AgentRuntimeExecutionStatuses.Succeeded, StringComparison.OrdinalIgnoreCase))
        {
            if (options.FallbackToRuleBasedOnRuntimeFailure)
            {
                var detail = runtimeResult.TraceNotes.Count > 0
                    ? string.Join(" ", runtimeResult.TraceNotes)
                    : "Mapping runtime execution failed.";
                diagnosticsBuilder?.SetRuntimeFailure(detail, usedRuleBasedFallback: true);
                var fallback = await ruleBasedMappingProvider.SuggestAsync(request, cancellationToken);
                return fallback with
                {
                    ProviderKey = ProviderKey,
                    Diagnostics = diagnosticsBuilder?.Build()
                };
            }

            var failureMessage = runtimeResult.TraceNotes.Count > 0
                ? string.Join(" ", runtimeResult.TraceNotes)
                : "Mapping runtime execution failed.";
            throw new RequestValidationException(failureMessage);
        }

        var structuredOutputJson = runtimeResult.StructuredOutputJson
            ?? throw new RequestValidationException("Mapping runtime did not return structured output.");

        diagnosticsBuilder?.SetRuntimeStructuredOutput(structuredOutputJson);

        var parsed = ParseRuntimeOutput(structuredOutputJson);
        MappingSuggestionOntologyValidator.Validate(
            parsed.ColumnSuggestions,
            parsed.LifecycleSuggestions,
            request.ModelContext);

        var columnSuggestions = parsed.ColumnSuggestions
            .Select(MappingSuggestionOntologyValidator.ClampColumn)
            .ToList();
        var lifecycleSuggestions = parsed.LifecycleSuggestions
            .Select(MappingSuggestionOntologyValidator.ClampLifecycle)
            .ToList();

        return new ImportMappingSuggestionResult(
            ProviderKey,
            columnSuggestions,
            lifecycleSuggestions,
            diagnosticsBuilder?.Build());
    }

    private async Task<PrefetchResult> PrefetchToolOutputsAsync(
        ActiveTenantContext context,
        ImportMappingSuggestionRequest request,
        MappingSuggestionOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.PrefetchToolEnabled || string.IsNullOrWhiteSpace(options.PrefetchToolKey))
        {
            return PrefetchResult.CreateSkipped(options.PrefetchToolKey);
        }

        var resolved = await publishedToolVersionResolver.TryResolvePublishedToolAsync(
            context.TenantId,
            options.PrefetchToolKey,
            cancellationToken);
        if (resolved is null)
        {
            return PrefetchResult.CreateNotFound(options.PrefetchToolKey);
        }

        var toolInputJson = JsonSerializer.Serialize(new
        {
            headers = request.Headers,
            sampleRows = request.SampleRows,
            modelPackageVersionId = request.ModelContext.ModelPackage.Id
        }, JsonOptions);

        try
        {
            var toolResponse = await toolGateway.ExecuteAsync(
                resolved.Value.ArtifactId,
                resolved.Value.VersionId,
                new ToolExecutionRequest(toolInputJson, ParentAgentRunId: null),
                cancellationToken);

            var summaries = JsonSerializer.Serialize(new List<object>
            {
                new
                {
                    toolDefinitionVersionId = resolved.Value.VersionId,
                    toolRunId = toolResponse.ToolRunId,
                    status = toolResponse.Status,
                    outputSafeSummaryJson = toolResponse.OutputSafeSummaryJson
                }
            }, JsonOptions);

            return PrefetchResult.CreateSucceeded(
                options.PrefetchToolKey,
                toolResponse.ToolRunId,
                toolResponse.Status,
                toolResponse.OutputSafeSummaryJson,
                summaries);
        }
        catch (Exception exception)
        {
            return PrefetchResult.CreateFailed(options.PrefetchToolKey, exception.Message);
        }
    }

    private static ParsedMappingOutput ParseRuntimeOutput(string structuredOutputJson)
    {
        MappingRuntimeOutputDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<MappingRuntimeOutputDocument>(structuredOutputJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new RequestValidationException($"Mapping runtime output is not valid JSON: {ex.Message}");
        }

        if (document is null)
        {
            throw new RequestValidationException("Mapping runtime output is empty.");
        }

        var columnSuggestions = (document.ColumnSuggestions ?? [])
            .Select(item => new ImportColumnMappingSuggestionResponse(
                item.SourceColumn ?? string.Empty,
                item.CanonicalObjectType ?? string.Empty,
                item.CanonicalAttributeKey,
                item.IsIdentityField,
                item.IsRequired,
                item.Confidence,
                item.Rationale ?? string.Empty))
            .ToList();

        var lifecycleSuggestions = (document.LifecycleSuggestions ?? [])
            .Select(item => new ImportLifecycleMappingSuggestionResponse(
                item.SourceValue ?? string.Empty,
                item.CanonicalLifecycleKey ?? string.Empty,
                item.Confidence,
                item.Rationale ?? string.Empty))
            .ToList();

        return new ParsedMappingOutput(columnSuggestions, lifecycleSuggestions);
    }

    private sealed record ParsedMappingOutput(
        IReadOnlyCollection<ImportColumnMappingSuggestionResponse> ColumnSuggestions,
        IReadOnlyCollection<ImportLifecycleMappingSuggestionResponse> LifecycleSuggestions);

    private sealed record PrefetchResult(
        bool Attempted,
        bool Succeeded,
        string? ToolKey,
        Guid? ToolRunId,
        string? Status,
        string? Error,
        string? ToolOutputJson,
        string? ToolOutputSummariesJson)
    {
        public static PrefetchResult CreateSkipped(string? toolKey) =>
            new(false, false, toolKey, null, null, null, null, null);

        public static PrefetchResult CreateNotFound(string? toolKey) =>
            new(true, false, toolKey, null, "NotFound", "Published tool was not found for tenant.", null, null);

        public static PrefetchResult CreateSucceeded(
            string toolKey,
            Guid toolRunId,
            string status,
            string? toolOutputJson,
            string summariesJson) =>
            new(true, true, toolKey, toolRunId, status, null, toolOutputJson, summariesJson);

        public static PrefetchResult CreateFailed(string? toolKey, string error) =>
            new(true, false, toolKey, null, "Failed", error, null, null);
    }

    private sealed class MappingSuggestionDiagnosticsBuilder(string providerKey, MappingSuggestionOptions options)
    {
        private string? _governedContextJson;
        private string? _structuredInputJson;
        private PrefetchResult? _prefetch;
        private AgentRuntimeExecutionRequest? _runtimeRequest;
        private AgentRuntimeExecutionResult? _runtimeResult;
        private string? _runtimeStructuredOutputJson;
        private bool _usedRuleBasedFallback;
        private string? _errorMessage;

        public void SetContextPayloads(string governedContextJson, string structuredInputJson)
        {
            _governedContextJson = governedContextJson;
            _structuredInputJson = structuredInputJson;
        }

        public void SetPrefetch(PrefetchResult prefetch) => _prefetch = prefetch;

        public void SetRuntimeRequest(AgentRuntimeExecutionRequest request, MappingSuggestionOptions mappingOptions)
        {
            _runtimeRequest = request;
            _ = mappingOptions;
        }

        public void SetRuntimeResult(AgentRuntimeExecutionResult result) => _runtimeResult = result;

        public void SetRuntimeStructuredOutput(string structuredOutputJson) =>
            _runtimeStructuredOutputJson = structuredOutputJson;

        public void SetRuntimeFailure(string errorMessage, bool usedRuleBasedFallback)
        {
            _errorMessage = errorMessage;
            _usedRuleBasedFallback = usedRuleBasedFallback;
        }

        public ImportMappingSuggestionDiagnostics Build()
        {
            var prefetch = _prefetch ?? PrefetchResult.CreateSkipped(options.PrefetchToolKey);
            return new ImportMappingSuggestionDiagnostics(
                providerKey,
                RuntimeCalled: _runtimeResult is not null || _runtimeRequest is not null,
                _runtimeRequest?.RequestedAdapterKey ?? options.RuntimeAdapterKey,
                _runtimeResult?.Status,
                _runtimeResult?.ModelUsed,
                _runtimeResult?.FallbackAppliedJson,
                _runtimeResult?.TraceNotes ?? [],
                prefetch.Attempted,
                prefetch.Succeeded,
                prefetch.ToolKey,
                prefetch.ToolRunId,
                prefetch.Status,
                prefetch.Error,
                prefetch.ToolOutputJson,
                _governedContextJson,
                _structuredInputJson,
                prefetch.ToolOutputSummariesJson,
                options.PromptTemplateBody,
                MappingSuggestionOutputSchema.Json,
                options.PrimaryModelProviderKey,
                options.PrimaryModelId,
                _runtimeStructuredOutputJson,
                _usedRuleBasedFallback,
                _errorMessage);
        }
    }

    private sealed class MappingRuntimeOutputDocument
    {
        [JsonPropertyName("columnSuggestions")]
        public List<MappingRuntimeColumnDocument>? ColumnSuggestions { get; set; }

        [JsonPropertyName("lifecycleSuggestions")]
        public List<MappingRuntimeLifecycleDocument>? LifecycleSuggestions { get; set; }
    }

    private sealed class MappingRuntimeColumnDocument
    {
        [JsonPropertyName("sourceColumn")]
        public string? SourceColumn { get; set; }

        [JsonPropertyName("canonicalObjectType")]
        public string? CanonicalObjectType { get; set; }

        [JsonPropertyName("canonicalAttributeKey")]
        public string? CanonicalAttributeKey { get; set; }

        [JsonPropertyName("isIdentityField")]
        public bool IsIdentityField { get; set; }

        [JsonPropertyName("isRequired")]
        public bool IsRequired { get; set; }

        [JsonPropertyName("confidence")]
        public decimal Confidence { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }
    }

    private sealed class MappingRuntimeLifecycleDocument
    {
        [JsonPropertyName("sourceValue")]
        public string? SourceValue { get; set; }

        [JsonPropertyName("canonicalLifecycleKey")]
        public string? CanonicalLifecycleKey { get; set; }

        [JsonPropertyName("confidence")]
        public decimal Confidence { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }
    }
}
