using System.Text.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Ontology;
using ETOS.Backend.Tests.Fixtures;
using ETOS.Backend.ToolRegistry;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class MappingSuggestionProviderTests
{
    private static readonly AgentExecutionProfile TestProfile = new(
        "import-mapping-assistant",
        "mapping-assistant",
        Guid.NewGuid(),
        Guid.NewGuid(),
        AgentRuntimeAdapterKeys.PydanticAi,
        "openai",
        "gpt-4o-mini",
        [],
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        null,
        [Guid.NewGuid()]);

    [Fact]
    public async Task RuleBasedProviderMatchesOntologyAttributesAndLifecycle()
    {
        var resolved = CreateResolvedContext();
        var provider = new RuleBasedMappingProvider();
        var result = await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(
                ["partNumber", "lifecycle"],
                [new Dictionary<string, string?> { ["partNumber"] = "P-1", ["lifecycle"] = "released" }],
                resolved),
            CancellationToken.None);

        Assert.Equal(MappingSuggestionProviderKeys.RuleBased, result.ProviderKey);
        var column = Assert.Single(result.ColumnSuggestions, item => item.SourceColumn == "partNumber");
        Assert.Equal("part", column.CanonicalObjectType);
        Assert.Equal("partNumber", column.CanonicalAttributeKey);
        var lifecycleSuggestion = Assert.Single(result.LifecycleSuggestions);
        Assert.Equal("released", lifecycleSuggestion.CanonicalLifecycleKey);
    }

    [Fact]
    public async Task PydanticAiProviderMapsValidRuntimeOutput()
    {
        var resolved = CreateResolvedContext();
        var runtimeAdapter = new RecordingAgentRuntimeAdapter(CreateValidMappingOutputJson());
        var provider = CreatePydanticAiProvider(
            runtimeAdapter,
            enabled: true);

        var result = await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(
                ["partNumber", "lifecycle"],
                [new Dictionary<string, string?> { ["partNumber"] = "P-1", ["lifecycle"] = "released" }],
                resolved),
            CancellationToken.None);

        Assert.Equal(MappingSuggestionProviderKeys.PydanticAi, result.ProviderKey);
        var column = Assert.Single(result.ColumnSuggestions, item => item.SourceColumn == "partNumber");
        Assert.Equal("part", column.CanonicalObjectType);
        Assert.Equal("LLM matched canonical attribute.", column.Rationale);
        Assert.NotNull(runtimeAdapter.LastRequest);
        Assert.True(runtimeAdapter.LastRequest!.PreviewMode);
        Assert.Null(runtimeAdapter.LastRequest.AgentRunId);
        Assert.Equal("openai", runtimeAdapter.LastRequest.PrimaryModelProviderKey);
        Assert.Equal("gpt-4o-mini", runtimeAdapter.LastRequest.PrimaryModelId);
    }

    [Fact]
    public async Task PydanticAiProviderRejectsInvalidOntologyWhenFallbackDisabled()
    {
        var resolved = CreateResolvedContext();
        var invalidOutput = """
            {
              "columnSuggestions": [
                {
                  "sourceColumn": "partNumber",
                  "canonicalObjectType": "unknown-type",
                  "canonicalAttributeKey": "partNumber",
                  "isIdentityField": true,
                  "isRequired": true,
                  "confidence": 0.9,
                  "rationale": "Invalid object type."
                }
              ],
              "lifecycleSuggestions": []
            }
            """;
        var provider = CreatePydanticAiProvider(
            new RecordingAgentRuntimeAdapter(invalidOutput),
            enabled: true,
            fallbackToRuleBased: false);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            provider.SuggestAsync(
                new ImportMappingSuggestionRequest(["partNumber"], [], resolved),
                CancellationToken.None));

        Assert.Contains("unknown object type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PydanticAiProviderFallsBackToRuleBasedOnInvalidOntologyWhenFallbackEnabled()
    {
        var resolved = CreateResolvedContext();
        var invalidOutput = """
            {
              "columnSuggestions": [
                {
                  "sourceColumn": "partNumber",
                  "canonicalObjectType": "unknown-type",
                  "canonicalAttributeKey": "partNumber",
                  "isIdentityField": true,
                  "isRequired": true,
                  "confidence": 0.9,
                  "rationale": "Invalid object type."
                }
              ],
              "lifecycleSuggestions": []
            }
            """;
        var provider = CreatePydanticAiProvider(
            new RecordingAgentRuntimeAdapter(invalidOutput),
            enabled: true,
            fallbackToRuleBased: true);

        var result = await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(
                ["partNumber", "lifecycle"],
                [new Dictionary<string, string?> { ["partNumber"] = "P-1", ["lifecycle"] = "released" }],
                resolved,
                IncludeDiagnostics: true),
            CancellationToken.None);

        Assert.Equal(MappingSuggestionProviderKeys.PydanticAi, result.ProviderKey);
        var column = Assert.Single(result.ColumnSuggestions, item => item.SourceColumn == "partNumber");
        Assert.Equal("partNumber", column.CanonicalAttributeKey);
        Assert.NotNull(result.Diagnostics);
        Assert.True(result.Diagnostics!.UsedRuleBasedFallback);
        Assert.Contains("unknown object type", result.Diagnostics.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PydanticAiProviderFallsBackToRuleBasedOnUnknownAttributeKey()
    {
        var resolved = CreateResolvedContext();
        var invalidOutput = """
            {
              "columnSuggestions": [
                {
                  "sourceColumn": "productCategory",
                  "canonicalObjectType": "part",
                  "canonicalAttributeKey": "productCategory",
                  "isIdentityField": false,
                  "isRequired": false,
                  "confidence": 0.9,
                  "rationale": "Copied source column name."
                }
              ],
              "lifecycleSuggestions": []
            }
            """;
        var provider = CreatePydanticAiProvider(
            new RecordingAgentRuntimeAdapter(invalidOutput),
            enabled: true,
            fallbackToRuleBased: true);

        var result = await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(
                ["productCategory"],
                [new Dictionary<string, string?> { ["productCategory"] = "All / Saleable" }],
                resolved,
                IncludeDiagnostics: true),
            CancellationToken.None);

        Assert.Equal(MappingSuggestionProviderKeys.PydanticAi, result.ProviderKey);
        Assert.NotNull(result.Diagnostics);
        Assert.True(result.Diagnostics!.UsedRuleBasedFallback);
        Assert.Contains("unknown attribute", result.Diagnostics.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.ColumnSuggestions,
            item => string.Equals(item.CanonicalAttributeKey, "productCategory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PydanticAiProviderPassesClosedEnumSchemaAndAllowListInput()
    {
        var resolved = CreateResolvedContext();
        var runtimeAdapter = new RecordingAgentRuntimeAdapter(CreateValidMappingOutputJson());
        var provider = CreatePydanticAiProvider(runtimeAdapter, enabled: true);

        await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(["partNumber"], [], resolved),
            CancellationToken.None);

        Assert.NotNull(runtimeAdapter.LastRequest);
        Assert.Contains("\"enum\"", runtimeAdapter.LastRequest!.OutputSchemaJson, StringComparison.Ordinal);
        Assert.Contains("partNumber", runtimeAdapter.LastRequest.OutputSchemaJson, StringComparison.Ordinal);
        Assert.Contains("allowedObjectTypes", runtimeAdapter.LastRequest.StructuredInputJson, StringComparison.Ordinal);
        Assert.Contains("allowedAttributes", runtimeAdapter.LastRequest.StructuredInputJson, StringComparison.Ordinal);
        Assert.Contains("allowedLifecycleKeys", runtimeAdapter.LastRequest.StructuredInputJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeClearsUnknownAttributeAndDropsUnknownObjectType()
    {
        var resolved = CreateResolvedContext();
        var result = MappingSuggestionOntologyValidator.Sanitize(
            [
                new ImportColumnMappingSuggestionResponse(
                    "productCategory",
                    "part",
                    "productCategory",
                    false,
                    false,
                    0.9m,
                    "Invented key."),
                new ImportColumnMappingSuggestionResponse(
                    "partNumber",
                    "unknown-type",
                    "partNumber",
                    true,
                    true,
                    0.9m,
                    "Bad type.")
            ],
            [
                new ImportLifecycleMappingSuggestionResponse("released", "released", 0.9m, "ok"),
                new ImportLifecycleMappingSuggestionResponse("bogus", "not-a-state", 0.5m, "bad")
            ],
            resolved);

        Assert.Equal(3, result.Issues.Count);
        Assert.Single(result.ColumnSuggestions);
        Assert.Null(result.ColumnSuggestions[0].CanonicalAttributeKey);
        Assert.True(result.ColumnSuggestions[0].Confidence <= 0.3m);
        Assert.Single(result.LifecycleSuggestions);
        Assert.Equal("released", result.LifecycleSuggestions[0].CanonicalLifecycleKey);
    }

    [Fact]
    public void OutputSchemaFactoryInjectsClosedEnums()
    {
        var resolved = CreateResolvedContext();
        var schemaJson = MappingSuggestionOutputSchemaFactory.Build(resolved);
        using var document = JsonDocument.Parse(schemaJson);
        var columnProps = document.RootElement
            .GetProperty("properties")
            .GetProperty("columnSuggestions")
            .GetProperty("items")
            .GetProperty("properties");
        var objectEnum = columnProps.GetProperty("canonicalObjectType").GetProperty("enum");
        var attributeEnum = columnProps.GetProperty("canonicalAttributeKey").GetProperty("enum");
        Assert.Contains(objectEnum.EnumerateArray(), item => item.GetString() == "part");
        Assert.Contains(attributeEnum.EnumerateArray(), item => item.GetString() == "partNumber");
    }

    [Fact]
    public async Task PydanticAiProviderThrowsWhenDisabled()
    {
        var provider = CreatePydanticAiProvider(
            new RecordingAgentRuntimeAdapter(CreateValidMappingOutputJson()),
            enabled: false);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            provider.SuggestAsync(
                new ImportMappingSuggestionRequest(["partNumber"], [], CreateResolvedContext()),
                CancellationToken.None));

        Assert.Contains("not enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PydanticAiProviderFallsBackToRuleBasedWhenOutputIncomplete()
    {
        var resolved = CreateResolvedContext();
        var incompleteOutput = """
            {
              "columnSuggestions": [
                {
                  "sourceColumn": "partNumber",
                  "canonicalObjectType": "part",
                  "isIdentityField": false,
                  "isRequired": true,
                  "confidence": 0.85,
                  "rationale": "Missing canonical attribute key."
                }
              ],
              "lifecycleSuggestions": [
                {
                  "sourceValue": "released",
                  "canonicalLifecycleKey": "released",
                  "confidence": 0.9,
                  "rationale": "Direct lifecycle match."
                }
              ]
            }
            """;
        var provider = CreatePydanticAiProvider(
            new RecordingAgentRuntimeAdapter(incompleteOutput),
            enabled: true,
            fallbackToRuleBased: true);

        var result = await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(
                ["partNumber", "lifecycle"],
                [new Dictionary<string, string?> { ["partNumber"] = "P-1", ["lifecycle"] = "released" }],
                resolved),
            CancellationToken.None);

        Assert.Equal(MappingSuggestionProviderKeys.PydanticAi, result.ProviderKey);
        var column = Assert.Single(result.ColumnSuggestions, item => item.SourceColumn == "partNumber");
        Assert.Equal("partNumber", column.CanonicalAttributeKey);
        Assert.True(column.IsIdentityField);
    }

    [Fact]
    public async Task PydanticAiProviderIncludesPrefetchToolOutputInRuntimeRequest()
    {
        var resolved = CreateResolvedContext();
        var runtimeAdapter = new RecordingAgentRuntimeAdapter(CreateValidMappingOutputJson());
        var toolGateway = new RecordingToolGateway("""{"providerKey":"rule-based-v1","columnSuggestions":[],"lifecycleSuggestions":[]}""");
        var toolVersionId = TestProfile.ReferencedToolDefinitionVersionIds.First();
        var provider = CreatePydanticAiProvider(
            runtimeAdapter,
            enabled: true,
            toolGateway: toolGateway,
            profile: TestProfile with { ReferencedToolDefinitionVersionIds = [toolVersionId] });

        await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(["partNumber"], [], resolved),
            CancellationToken.None);

        Assert.NotNull(toolGateway.LastInputJson);
        Assert.Contains(resolved.ModelPackage.Id.ToString(), toolGateway.LastInputJson, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(runtimeAdapter.LastRequest?.ToolOutputSummariesJson);
        Assert.Contains(toolVersionId.ToString(), runtimeAdapter.LastRequest!.ToolOutputSummariesJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MappingPredictorToolHandlerReturnsRuleBasedOutput()
    {
        var resolved = CreateResolvedContext();
        var handler = new MappingPredictorToolHandler(new StubModelPackageContextResolver(resolved));
        var inputJson = JsonSerializer.Serialize(new
        {
            headers = new[] { "partNumber", "lifecycle" },
            sampleRows = new[]
            {
                new Dictionary<string, string?> { ["partNumber"] = "P-1", ["lifecycle"] = "released" }
            },
            modelPackageVersionId = resolved.ModelPackage.Id
        });

        var result = await handler.ExecuteAsync(
            new ToolHandlerContext(
                Guid.NewGuid(),
                Guid.NewGuid(),
                inputJson,
                new ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument
                {
                    ToolKey = "mapping-predictor-tool",
                    InternalHandlerKey = ToolInternalHandlerKeys.MappingPredictor,
                    OutputSchemaJson = "{}"
                },
                null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        using var document = JsonDocument.Parse(result.OutputSafeSummaryJson);
        Assert.Equal(MappingSuggestionProviderKeys.RuleBased, document.RootElement.GetProperty("providerKey").GetString());
        Assert.True(document.RootElement.GetProperty("columnSuggestions").GetArrayLength() >= 1);
    }

    private static PydanticAiMappingProvider CreatePydanticAiProvider(
        IAgentRuntimeAdapter runtimeAdapter,
        bool enabled,
        IToolGateway? toolGateway = null,
        AgentExecutionProfile? profile = null,
        bool fallbackToRuleBased = false)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var executionProfile = profile ?? TestProfile;
        return new PydanticAiMappingProvider(
            new StubAgentExecutionProfileResolver(executionProfile),
            new StubAgentRuntimePreviewOrchestrator(runtimeAdapter, toolGateway ?? new NoOpToolGateway(), executionProfile),
            Options.Create(new MappingSuggestionOptions
            {
                Enabled = enabled,
                MappingAssistantAgentKey = executionProfile.AgentKey,
                FallbackToRuleBasedOnRuntimeFailure = fallbackToRuleBased
            }),
            Options.Create(new AgentRuntimeOptions { BaseUrl = "http://localhost:8010", TimeoutSeconds = 30 }),
            new StubTenantContextResolver(new ActiveTenantContext(tenantId, "local", "Local", userId)),
            new RuleBasedMappingProvider());
    }

    private static string CreateValidMappingOutputJson() =>
        """
        {
          "columnSuggestions": [
            {
              "sourceColumn": "partNumber",
              "canonicalObjectType": "part",
              "canonicalAttributeKey": "partNumber",
              "isIdentityField": true,
              "isRequired": true,
              "confidence": 0.95,
              "rationale": "LLM matched canonical attribute."
            }
          ],
          "lifecycleSuggestions": [
            {
              "sourceValue": "released",
              "canonicalLifecycleKey": "released",
              "confidence": 0.9,
              "rationale": "Direct lifecycle match."
            }
          ]
        }
        """;

    private static ResolvedModelPackageContext CreateResolvedContext()
    {
        var tenantId = Guid.NewGuid();
        var ontology = new OntologyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "demo",
            NormalizedKey = "demo",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            ObjectTypes =
            [
                new OntologyObjectTypeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = "part",
                    NormalizedKey = "part",
                    DisplayName = "Part",
                    SafeSummary = "Part"
                }
            ]
        };
        var lifecycle = new LifecycleVocabularyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "lifecycle",
            NormalizedKey = "lifecycle",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            States =
            [
                new LifecycleStateDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = "released",
                    NormalizedKey = "released",
                    DisplayName = "Released",
                    SortOrder = 1
                }
            ]
        };
        var attributeSchema = new AttributeSchemaVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "attributes",
            NormalizedKey = "attributes",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Attributes =
            [
                new AttributeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AttributeKey = "partNumber",
                    NormalizedAttributeKey = "partnumber",
                    AppliesToObjectType = "part",
                    NormalizedAppliesToObjectType = "part",
                    IsRequired = true,
                    SafeSummary = "Part number"
                }
            ]
        };
        var modelPackage = new ModelPackageVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "pkg",
            NormalizedKey = "pkg",
            Name = "Package",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            ImportProfileJson = ReferencePackageTestProfiles.ImportProfileJson,
            QueryIntentExtensionsJson = ReferencePackageTestProfiles.QueryIntentExtensionsJson
        };
        var semanticLayer = new SemanticLayerVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "semantic",
            NormalizedKey = "semantic",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            GraphNodeTypeMappingsJson = """{"part":"part"}""",
            GraphRelationshipTypeMappingsJson = """{"contains":"BOM_CONTAINS"}"""
        };

        return new ResolvedModelPackageContext(
            modelPackage,
            ontology,
            semanticLayer,
            lifecycle,
            attributeSchema,
            ModelPackageProfileParser.ParseImportProfile(modelPackage.ImportProfileJson),
            ModelPackageProfileParser.ParseQueryIntentExtensions(modelPackage.QueryIntentExtensionsJson),
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphNodeTypeMappingsJson),
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphRelationshipTypeMappingsJson),
            ontology.BomRelationships.FirstOrDefault());
    }

    private sealed class StubAgentExecutionProfileResolver(AgentExecutionProfile profile) : IAgentExecutionProfileResolver
    {
        public Task<AgentExecutionProfile> ResolveByAgentKeyAsync(Guid tenantId, string agentKey, CancellationToken cancellationToken)
            => Task.FromResult(profile);

        public Task<AgentExecutionProfile> ResolveByAgentVersionIdAsync(Guid tenantId, Guid agentVersionId, CancellationToken cancellationToken)
            => Task.FromResult(profile);

        public Task<AgentExecutionProfile> ResolveMappingAssistantAsync(
            Guid tenantId,
            ResolvedModelPackageContext modelContext,
            string? agentKeyOverride = null,
            Guid? agentVersionIdOverride = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(profile);
    }

    private sealed class StubAgentRuntimePreviewOrchestrator(
        IAgentRuntimeAdapter runtimeAdapter,
        IToolGateway toolGateway,
        AgentExecutionProfile profile) : IAgentRuntimePreviewOrchestrator
    {
        public async Task<AgentRuntimePreviewOrchestratorResult> RunPreviewAsync(
            AgentExecutionProfile executionProfile,
            AgentRuntimePreviewInput input,
            CancellationToken cancellationToken)
        {
            var toolPrefetchSummaries = new List<AgentRuntimeToolPrefetchSummary>();
            var toolOutputSummaries = new List<object>();
            foreach (var toolVersionId in profile.ReferencedToolDefinitionVersionIds)
            {
                var toolResponse = await toolGateway.ExecuteAsync(
                    Guid.NewGuid(),
                    toolVersionId,
                    new ToolExecutionRequest(input.BuildToolInputJson(toolVersionId), null),
                    cancellationToken);
                toolPrefetchSummaries.Add(new AgentRuntimeToolPrefetchSummary(
                    toolVersionId,
                    toolResponse.ToolRunId,
                    toolResponse.Status,
                    toolResponse.OutputSafeSummaryJson,
                    null));
                toolOutputSummaries.Add(new
                {
                    toolDefinitionVersionId = toolVersionId,
                    toolRunId = toolResponse.ToolRunId,
                    status = toolResponse.Status,
                    outputSafeSummaryJson = toolResponse.OutputSafeSummaryJson
                });
            }

            var runtimeRequest = new AgentRuntimeExecutionRequest(
                input.TenantId,
                input.UserId,
                profile.SourceAgentTemplateVersionId,
                input.GovernedContextSummaryJson,
                input.StructuredInputJson,
                input.PreviewMode,
                profile.PreferredRuntimeAdapterKey,
                profile.AgentVersionId,
                input.AgentRunId,
                "prompt-body",
                input.OutputSchemaJsonOverride ?? MappingSuggestionOutputSchema.Json,
                profile.PrimaryModelProviderKey,
                profile.PrimaryModelId,
                "[]",
                JsonSerializer.Serialize(toolOutputSummaries));

            var runtimeResult = await runtimeAdapter.ExecuteAsync(runtimeRequest, cancellationToken);
            return new AgentRuntimePreviewOrchestratorResult(
                runtimeResult,
                "prompt-body",
                input.OutputSchemaJsonOverride ?? MappingSuggestionOutputSchema.Json,
                JsonSerializer.Serialize(toolOutputSummaries),
                toolPrefetchSummaries);
        }
    }

    private sealed class RecordingAgentRuntimeAdapter(string structuredOutputJson) : IAgentRuntimeAdapter
    {
        public AgentRuntimeExecutionRequest? LastRequest { get; private set; }

        public string AdapterKey => AgentRuntimeAdapterKeys.PydanticAi;

        public Task<AgentRuntimeExecutionResult> ExecuteAsync(
            AgentRuntimeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new AgentRuntimeExecutionResult(
                AdapterKey,
                AgentRuntimeExecutionStatuses.Succeeded,
                structuredOutputJson,
                ["mock-mapping-runtime"],
                "openai:gpt-4o-mini",
                null));
        }
    }

    private sealed class StubTenantContextResolver(ActiveTenantContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
            => Task.FromResult(context);
    }

    private sealed class RecordingToolGateway(string outputJson) : IToolGateway
    {
        public string? LastInputJson { get; private set; }

        public Task<ToolExecutionResponse> DryRunAsync(
            Guid artifactId,
            Guid versionId,
            ToolExecutionRequest request,
            CancellationToken cancellationToken)
            => ExecuteAsync(artifactId, versionId, request, cancellationToken);

        public Task<ToolExecutionResponse> ExecuteAsync(
            Guid artifactId,
            Guid versionId,
            ToolExecutionRequest request,
            CancellationToken cancellationToken)
        {
            LastInputJson = request.InputJson;
            return Task.FromResult(new ToolExecutionResponse(
                Guid.NewGuid(),
                ToolRunStatuses.Succeeded,
                outputJson,
                null,
                null,
                []));
        }
    }

    private sealed class NoOpToolGateway : IToolGateway
    {
        public Task<ToolExecutionResponse> DryRunAsync(
            Guid artifactId,
            Guid versionId,
            ToolExecutionRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(new ToolExecutionResponse(
                Guid.NewGuid(),
                ToolRunStatuses.Succeeded,
                "{}",
                null,
                null,
                []));

        public Task<ToolExecutionResponse> ExecuteAsync(
            Guid artifactId,
            Guid versionId,
            ToolExecutionRequest request,
            CancellationToken cancellationToken)
            => DryRunAsync(artifactId, versionId, request, cancellationToken);
    }

    private sealed class StubModelPackageContextResolver(ResolvedModelPackageContext context) : IModelPackageContextResolver
    {
        public Task<ResolvedModelPackageContext> ResolvePublishedAsync(
            Guid modelPackageVersionId,
            ActiveTenantContext tenantContext,
            string action,
            CancellationToken cancellationToken)
            => Task.FromResult(context);

        public Task<ResolvedModelPackageContext?> ResolveActivePublishedAsync(
            ActiveTenantContext tenantContext,
            string? packageKey,
            CancellationToken cancellationToken)
            => Task.FromResult<ResolvedModelPackageContext?>(context);
    }
}
