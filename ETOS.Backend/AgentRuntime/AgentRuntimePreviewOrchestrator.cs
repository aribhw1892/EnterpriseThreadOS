using System.Text.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AgentRuntime;

public sealed record AgentRuntimeToolPrefetchSummary(
    Guid ToolDefinitionVersionId,
    Guid ToolRunId,
    string Status,
    string? OutputSafeSummaryJson,
    string? Error);

public sealed record AgentRuntimePreviewInput(
    Guid TenantId,
    Guid UserId,
    string? GovernedContextSummaryJson,
    string? StructuredInputJson,
    bool PreviewMode,
    bool ToolDryRun,
    Guid? AgentVersionId,
    Guid? AgentRunId,
    Func<Guid, string> BuildToolInputJson,
    string? OutputSchemaJsonOverride = null);

public sealed record AgentRuntimePreviewOrchestratorResult(
    AgentRuntimeExecutionResult RuntimeResult,
    string PromptTemplatePayloadJson,
    string OutputSchemaJson,
    string ToolOutputSummariesJson,
    IReadOnlyCollection<AgentRuntimeToolPrefetchSummary> ToolPrefetchSummaries);

public interface IAgentRuntimePreviewOrchestrator
{
    Task<AgentRuntimePreviewOrchestratorResult> RunPreviewAsync(
        AgentExecutionProfile profile,
        AgentRuntimePreviewInput input,
        CancellationToken cancellationToken);
}

public sealed class AgentRuntimePreviewOrchestrator(
    EnterpriseThreadDbContext dbContext,
    IToolGateway toolGateway,
    IAgentRuntimeAdapterSelector adapterSelector) : IAgentRuntimePreviewOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentRuntimePreviewOrchestratorResult> RunPreviewAsync(
        AgentExecutionProfile profile,
        AgentRuntimePreviewInput input,
        CancellationToken cancellationToken)
    {
        var toolPrefetchSummaries = new List<AgentRuntimeToolPrefetchSummary>();
        var toolOutputSummaries = new List<object>();

        foreach (var toolVersionId in profile.ReferencedToolDefinitionVersionIds)
        {
            var toolVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == toolVersionId && item.TenantId == input.TenantId, cancellationToken)
                ?? throw new RequestValidationException($"Referenced tool version '{toolVersionId}' was not found.");

            var toolInputJson = input.BuildToolInputJson(toolVersionId);
            var toolRequest = new ToolExecutionRequest(toolInputJson, input.AgentRunId);
            ToolExecutionResponse toolResponse;
            try
            {
                toolResponse = input.ToolDryRun
                    ? await toolGateway.DryRunAsync(toolVersion.ArtifactId, toolVersion.Id, toolRequest, cancellationToken)
                    : await toolGateway.ExecuteAsync(toolVersion.ArtifactId, toolVersion.Id, toolRequest, cancellationToken);
            }
            catch (Exception exception)
            {
                toolPrefetchSummaries.Add(new AgentRuntimeToolPrefetchSummary(
                    toolVersionId,
                    Guid.Empty,
                    "Failed",
                    null,
                    exception.Message));
                continue;
            }

            toolPrefetchSummaries.Add(new AgentRuntimeToolPrefetchSummary(
                toolVersionId,
                toolResponse.ToolRunId,
                toolResponse.Status,
                toolResponse.OutputSafeSummaryJson,
                null));
            toolOutputSummaries.Add(new
            {
                toolDefinitionVersionId = toolVersion.Id,
                toolRunId = toolResponse.ToolRunId,
                status = toolResponse.Status,
                outputSafeSummaryJson = toolResponse.OutputSafeSummaryJson
            });
        }

        var promptTemplatePayloadJson = await LoadArtifactPayloadAsync(input.TenantId, profile.PromptTemplateVersionId, cancellationToken);
        var outputSchemaJson = !string.IsNullOrWhiteSpace(input.OutputSchemaJsonOverride)
            ? input.OutputSchemaJsonOverride
            : await LoadArtifactPayloadAsync(input.TenantId, profile.OutputSchemaVersionId, cancellationToken);
        var fallbackModelsJson = JsonSerializer.Serialize(profile.FallbackModels, JsonOptions);
        var toolOutputSummariesJson = JsonSerializer.Serialize(toolOutputSummaries, JsonOptions);

        var runtimeRequest = new AgentRuntimeExecutionRequest(
            input.TenantId,
            input.UserId,
            profile.SourceAgentTemplateVersionId,
            input.GovernedContextSummaryJson,
            input.StructuredInputJson,
            input.PreviewMode,
            profile.PreferredRuntimeAdapterKey,
            input.AgentVersionId ?? profile.AgentVersionId,
            input.AgentRunId,
            promptTemplatePayloadJson,
            outputSchemaJson,
            profile.PrimaryModelProviderKey,
            profile.PrimaryModelId,
            fallbackModelsJson,
            toolOutputSummariesJson);

        var runtimeResult = await adapterSelector.ExecuteAsync(runtimeRequest, cancellationToken);
        return new AgentRuntimePreviewOrchestratorResult(
            runtimeResult,
            promptTemplatePayloadJson,
            outputSchemaJson,
            toolOutputSummariesJson,
            toolPrefetchSummaries);
    }

    private async Task<string> LoadArtifactPayloadAsync(Guid tenantId, Guid versionId, CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId && item.TenantId == tenantId, cancellationToken)
            ?? throw new RequestValidationException($"Referenced artifact version '{versionId}' was not found.");
        return version.PayloadJson ?? "{}";
    }
}
