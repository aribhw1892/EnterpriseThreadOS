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
            enabled: true,
            prefetchEnabled: false);

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
    }

    [Fact]
    public async Task PydanticAiProviderRejectsInvalidOntologyInRuntimeOutput()
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
            prefetchEnabled: false);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            provider.SuggestAsync(
                new ImportMappingSuggestionRequest(["partNumber"], [], resolved),
                CancellationToken.None));

        Assert.Contains("unknown object type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PydanticAiProviderThrowsWhenDisabled()
    {
        var provider = CreatePydanticAiProvider(
            new RecordingAgentRuntimeAdapter(CreateValidMappingOutputJson()),
            enabled: false,
            prefetchEnabled: false);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            provider.SuggestAsync(
                new ImportMappingSuggestionRequest(["partNumber"], [], CreateResolvedContext()),
                CancellationToken.None));

        Assert.Contains("not enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PydanticAiProviderIncludesPrefetchToolOutputInRuntimeRequest()
    {
        var resolved = CreateResolvedContext();
        var runtimeAdapter = new RecordingAgentRuntimeAdapter(CreateValidMappingOutputJson());
        var toolGateway = new RecordingToolGateway("""{"providerKey":"rule-based-v1","columnSuggestions":[],"lifecycleSuggestions":[]}""");
        var toolVersionId = Guid.NewGuid();
        var toolArtifactId = Guid.NewGuid();
        var provider = CreatePydanticAiProvider(
            runtimeAdapter,
            enabled: true,
            prefetchEnabled: true,
            toolGateway: toolGateway,
            publishedToolResolver: new StubPublishedToolVersionResolver(toolArtifactId, toolVersionId));

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
        bool prefetchEnabled,
        IToolGateway? toolGateway = null,
        IPublishedToolVersionResolver? publishedToolResolver = null)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        return new PydanticAiMappingProvider(
            new StubAgentRuntimeAdapterSelector(runtimeAdapter),
            Options.Create(new MappingSuggestionOptions
            {
                Enabled = enabled,
                PrefetchToolEnabled = prefetchEnabled,
                PrefetchToolKey = "mapping-predictor-tool",
                PrimaryModelProviderKey = "openai",
                PrimaryModelId = "gpt-4o-mini"
            }),
            Options.Create(new AgentRuntimeOptions { BaseUrl = "http://localhost:8010", TimeoutSeconds = 30 }),
            new StubTenantContextResolver(new ActiveTenantContext(tenantId, "local", "Local", userId)),
            new RuleBasedMappingProvider(),
            toolGateway ?? new NoOpToolGateway(),
            publishedToolResolver ?? new StubPublishedToolVersionResolver(null, null));
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

    private sealed class StubAgentRuntimeAdapterSelector(IAgentRuntimeAdapter adapter) : IAgentRuntimeAdapterSelector
    {
        public IAgentRuntimeAdapter Resolve(string adapterKey) => adapter;

        public Task<AgentRuntimeExecutionResult> ExecuteAsync(
            AgentRuntimeExecutionRequest request,
            CancellationToken cancellationToken)
            => adapter.ExecuteAsync(request, cancellationToken);
    }

    private sealed class StubTenantContextResolver(ActiveTenantContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
            => Task.FromResult(context);
    }

    private sealed class StubPublishedToolVersionResolver(Guid? artifactId, Guid? versionId) : IPublishedToolVersionResolver
    {
        public Task<(Guid ArtifactId, Guid VersionId)?> TryResolvePublishedToolAsync(
            Guid tenantId,
            string toolKey,
            CancellationToken cancellationToken)
            => Task.FromResult(artifactId is null || versionId is null
                ? ((Guid ArtifactId, Guid VersionId)?)null
                : (artifactId.Value, versionId.Value));
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
