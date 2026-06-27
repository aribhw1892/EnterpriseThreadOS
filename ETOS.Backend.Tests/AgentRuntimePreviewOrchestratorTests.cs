using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class AgentRuntimePreviewOrchestratorTests
{
    [Fact]
    public async Task RunPreviewAsync_ExecutesReferencedToolsAndCallsRuntimeAdapter()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolArtifactId = Guid.NewGuid();
        var toolVersionId = Guid.NewGuid();
        var promptVersionId = Guid.NewGuid();
        var schemaVersionId = Guid.NewGuid();

        await using var dbContext = CreateDbContext(tenantId, userId, toolArtifactId, toolVersionId, promptVersionId, schemaVersionId);
        var toolGateway = new RecordingToolGateway();
        var runtimeAdapter = new RecordingRuntimeAdapter();
        var orchestrator = new AgentRuntimePreviewOrchestrator(
            dbContext,
            toolGateway,
            new StubAdapterSelector(runtimeAdapter));

        var profile = new AgentExecutionProfile(
            "import-mapping-assistant",
            "mapping-assistant",
            Guid.NewGuid(),
            null,
            AgentRuntimeAdapterKeys.PydanticAi,
            "openai",
            "gpt-4o-mini",
            [],
            promptVersionId,
            schemaVersionId,
            null,
            null,
            [toolVersionId]);

        var result = await orchestrator.RunPreviewAsync(
            profile,
            new AgentRuntimePreviewInput(
                tenantId,
                userId,
                "{}",
                """{"headers":["partNumber"]}""",
                PreviewMode: true,
                ToolDryRun: false,
                profile.AgentVersionId,
                null,
                _ => """{"headers":["partNumber"]}"""),
            CancellationToken.None);

        Assert.Equal(AgentRuntimeExecutionStatuses.Succeeded, result.RuntimeResult.Status);
        Assert.NotNull(toolGateway.LastInputJson);
        Assert.Contains("partNumber", toolGateway.LastInputJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(toolVersionId.ToString(), result.ToolOutputSummariesJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("openai", runtimeAdapter.LastRequest?.PrimaryModelProviderKey);
    }

    private static EnterpriseThreadDbContext CreateDbContext(
        Guid tenantId,
        Guid userId,
        Guid toolArtifactId,
        Guid toolVersionId,
        Guid promptVersionId,
        Guid schemaVersionId)
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new EnterpriseThreadDbContext(options);

        dbContext.Artifacts.AddRange(
            new Artifact
            {
                Id = toolArtifactId,
                TenantId = tenantId,
                ArtifactType = ToolDefinitionArtifactTypes.ToolDefinition,
                NormalizedArtifactType = ToolDefinitionArtifactTypes.ToolDefinition.ToUpperInvariant(),
                Name = "mapping-predictor-tool",
                OwnerUserId = userId,
                LifecycleState = ArtifactLifecycleState.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var promptArtifactId = Guid.NewGuid();
        var schemaArtifactId = Guid.NewGuid();
        dbContext.Artifacts.AddRange(
            new Artifact
            {
                Id = promptArtifactId,
                TenantId = tenantId,
                ArtifactType = "PromptTemplateVersion",
                NormalizedArtifactType = "PROMPTTEMPLATEVERSION",
                Name = "platform-import-mapping",
                OwnerUserId = userId,
                LifecycleState = ArtifactLifecycleState.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Artifact
            {
                Id = schemaArtifactId,
                TenantId = tenantId,
                ArtifactType = "OutputSchemaVersion",
                NormalizedArtifactType = "OUTPUTSCHEMAVERSION",
                Name = "import-mapping-suggestion-schema",
                OwnerUserId = userId,
                LifecycleState = ArtifactLifecycleState.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        dbContext.ArtifactVersions.AddRange(
            new ArtifactVersion
            {
                Id = toolVersionId,
                TenantId = tenantId,
                ArtifactId = toolArtifactId,
                VersionLabel = "v1",
                NormalizedVersionLabel = "V1",
                PayloadJson = "{}",
                ReadinessState = ArtifactReadinessState.Published,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new ArtifactVersion
            {
                Id = promptVersionId,
                TenantId = tenantId,
                ArtifactId = promptArtifactId,
                VersionLabel = "v1",
                NormalizedVersionLabel = "V1",
                PayloadJson = """{"body":"mapping prompt"}""",
                ReadinessState = ArtifactReadinessState.Published,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new ArtifactVersion
            {
                Id = schemaVersionId,
                TenantId = tenantId,
                ArtifactId = schemaArtifactId,
                VersionLabel = "v1",
                NormalizedVersionLabel = "V1",
                PayloadJson = MappingSuggestionOutputSchema.Json,
                ReadinessState = ArtifactReadinessState.Published,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });

        dbContext.SaveChanges();
        return dbContext;
    }

    private sealed class RecordingToolGateway : IToolGateway
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
                """{"providerKey":"rule-based-v1"}""",
                null,
                null,
                []));
        }
    }

    private sealed class RecordingRuntimeAdapter : IAgentRuntimeAdapter
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
                """{"columnSuggestions":[],"lifecycleSuggestions":[]}""",
                [],
                "openai:gpt-4o-mini",
                null));
        }
    }

    private sealed class StubAdapterSelector(IAgentRuntimeAdapter adapter) : IAgentRuntimeAdapterSelector
    {
        public IAgentRuntimeAdapter Resolve(string adapterKey) => adapter;

        public Task<AgentRuntimeExecutionResult> ExecuteAsync(
            AgentRuntimeExecutionRequest request,
            CancellationToken cancellationToken)
            => adapter.ExecuteAsync(request, cancellationToken);
    }
}
