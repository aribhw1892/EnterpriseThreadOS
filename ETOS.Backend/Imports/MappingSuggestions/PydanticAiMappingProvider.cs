using System.Text.Json;

using System.Text.Json.Serialization;

using ETOS.Backend.AgentRuntime;

using ETOS.Backend.Identity;

using Microsoft.Extensions.Options;



using ETOS.Backend.ToolRegistry;

namespace ETOS.Backend.Imports.MappingSuggestions;



public sealed class PydanticAiMappingProvider(

    IAgentExecutionProfileResolver profileResolver,

    IAgentRuntimePreviewOrchestrator previewOrchestrator,

    IOptions<MappingSuggestionOptions> mappingOptions,

    IOptions<AgentRuntimeOptions> runtimeOptions,

    ITenantContextResolver tenantContextResolver,

    RuleBasedMappingProvider ruleBasedMappingProvider) : IMappingSuggestionProvider

{

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);



    public string ProviderKey => MappingSuggestionProviderKeys.PydanticAi;



    public async Task<ImportMappingSuggestionResult> SuggestAsync(

        ImportMappingSuggestionRequest request,

        CancellationToken cancellationToken)

    {

        var options = mappingOptions.Value;

        var includeDiagnostics = request.IncludeDiagnostics;



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

        AgentExecutionProfile profile;

        try

        {

            profile = await profileResolver.ResolveMappingAssistantAsync(

                context.TenantId,

                request.ModelContext,

                request.MappingAssistantAgentKey,

                request.MappingAssistantAgentVersionId,

                cancellationToken);

        }

        catch (RequestValidationException exception) when (options.FallbackToRuleBasedOnRuntimeFailure)

        {

            var fallback = await ruleBasedMappingProvider.SuggestAsync(request, cancellationToken);

            return fallback with

            {

                ProviderKey = ProviderKey,

                Diagnostics = includeDiagnostics

                    ? BuildFailureDiagnostics(ProviderKey, profile: null, exception.Message, usedRuleBasedFallback: true)

                    : null

            };

        }



        var diagnosticsBuilder = includeDiagnostics

            ? new MappingSuggestionDiagnosticsBuilder(ProviderKey, profile)

            : null;



        var governedContextJson = MappingSuggestionContextBuilder.BuildGovernedContextJson(request.ModelContext);

        var structuredInputJson = MappingSuggestionContextBuilder.BuildStructuredInputJson(request);

        diagnosticsBuilder?.SetContextPayloads(governedContextJson, structuredInputJson);



        var toolInputJson = JsonSerializer.Serialize(new

        {

            headers = request.Headers,

            sampleRows = request.SampleRows,

            modelPackageVersionId = request.ModelContext.ModelPackage.Id

        }, JsonOptions);



        AgentRuntimePreviewOrchestratorResult orchestratorResult;

        try

        {

            orchestratorResult = await previewOrchestrator.RunPreviewAsync(

                profile,

                new AgentRuntimePreviewInput(

                    context.TenantId,

                    context.UserId,

                    governedContextJson,

                    structuredInputJson,

                    PreviewMode: true,

                    ToolDryRun: false,

                    profile.AgentVersionId,

                    AgentRunId: null,

                    _ => toolInputJson),

                cancellationToken);

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



        diagnosticsBuilder?.SetOrchestratorResult(orchestratorResult, profile);



        var runtimeResult = orchestratorResult.RuntimeResult;

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

        if (options.FallbackToRuleBasedOnRuntimeFailure
            && !MappingSuggestionOutputQuality.HasUsableColumnSuggestions(parsed.ColumnSuggestions))
        {
            diagnosticsBuilder?.SetRuntimeFailure(
                "Structured mapping output did not include usable column attribute keys.",
                usedRuleBasedFallback: true);
            var fallback = await ruleBasedMappingProvider.SuggestAsync(request, cancellationToken);
            return fallback with
            {
                ProviderKey = ProviderKey,
                Diagnostics = diagnosticsBuilder?.Build()
            };
        }

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



    private static ImportMappingSuggestionDiagnostics BuildFailureDiagnostics(

        string providerKey,

        AgentExecutionProfile? profile,

        string errorMessage,

        bool usedRuleBasedFallback)

        => new(

            providerKey,

            profile?.AgentKey,

            RuntimeCalled: false,

            profile?.PreferredRuntimeAdapterKey,

            null,

            null,

            null,

            [],

            PrefetchAttempted: false,

            PrefetchSucceeded: false,

            null,

            null,

            null,

            null,

            null,

            null,

            null,

            null,

            null,

            null,

            profile?.PrimaryModelProviderKey,

            profile?.PrimaryModelId,

            null,

            usedRuleBasedFallback,

            errorMessage);



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



    private sealed class MappingSuggestionDiagnosticsBuilder(string providerKey, AgentExecutionProfile profile)

    {

        private string? _governedContextJson;

        private string? _structuredInputJson;

        private AgentRuntimePreviewOrchestratorResult? _orchestratorResult;

        private string? _runtimeStructuredOutputJson;

        private bool _usedRuleBasedFallback;

        private string? _errorMessage;



        public void SetContextPayloads(string governedContextJson, string structuredInputJson)

        {

            _governedContextJson = governedContextJson;

            _structuredInputJson = structuredInputJson;

        }



        public void SetOrchestratorResult(AgentRuntimePreviewOrchestratorResult result, AgentExecutionProfile executionProfile)

        {

            _orchestratorResult = result;

            _ = executionProfile;

        }



        public void SetRuntimeStructuredOutput(string structuredOutputJson) =>

            _runtimeStructuredOutputJson = structuredOutputJson;



        public void SetRuntimeFailure(string errorMessage, bool usedRuleBasedFallback)

        {

            _errorMessage = errorMessage;

            _usedRuleBasedFallback = usedRuleBasedFallback;

        }



        public ImportMappingSuggestionDiagnostics Build()

        {

            var runtimeResult = _orchestratorResult?.RuntimeResult;

            var firstPrefetch = _orchestratorResult?.ToolPrefetchSummaries.FirstOrDefault();

            var prefetchAttempted = (_orchestratorResult?.ToolPrefetchSummaries.Count ?? 0) > 0;

            var prefetchSucceeded = _orchestratorResult?.ToolPrefetchSummaries.Any(item =>

                string.Equals(item.Status, ToolRunStatuses.Succeeded, StringComparison.OrdinalIgnoreCase)) == true;



            return new ImportMappingSuggestionDiagnostics(

                providerKey,

                profile.AgentKey,

                RuntimeCalled: runtimeResult is not null || _orchestratorResult is not null,

                profile.PreferredRuntimeAdapterKey,

                runtimeResult?.Status,

                runtimeResult?.ModelUsed,

                runtimeResult?.FallbackAppliedJson,

                runtimeResult?.TraceNotes ?? [],

                prefetchAttempted,

                prefetchSucceeded,

                profile.ReferencedToolDefinitionVersionIds.Count > 0 ? "mapping-predictor-tool" : null,

                firstPrefetch?.ToolRunId == Guid.Empty ? null : firstPrefetch?.ToolRunId,

                firstPrefetch?.Status,

                firstPrefetch?.Error,

                firstPrefetch?.OutputSafeSummaryJson,

                _governedContextJson,

                _structuredInputJson,

                _orchestratorResult?.ToolOutputSummariesJson,

                _orchestratorResult?.PromptTemplatePayloadJson,

                _orchestratorResult?.OutputSchemaJson,

                profile.PrimaryModelProviderKey,

                profile.PrimaryModelId,

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


