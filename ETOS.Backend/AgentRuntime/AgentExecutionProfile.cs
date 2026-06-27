using ETOS.Backend.Agents;
using ETOS.Backend.AgentTemplates;

namespace ETOS.Backend.AgentRuntime;

public sealed record AgentExecutionProfile(
    string AgentKey,
    string PatternCategory,
    Guid? AgentVersionId,
    Guid? SourceAgentTemplateVersionId,
    string PreferredRuntimeAdapterKey,
    string PrimaryModelProviderKey,
    string PrimaryModelId,
    IReadOnlyCollection<AgentDefinitionPayloadParser.FallbackModelDocument> FallbackModels,
    Guid PromptTemplateVersionId,
    Guid OutputSchemaVersionId,
    Guid? QueryIntentVersionId,
    Guid? RetrievalStrategyVersionId,
    IReadOnlyCollection<Guid> ReferencedToolDefinitionVersionIds)
{
    public static AgentExecutionProfile FromAgentPayload(
        string agentKey,
        string patternCategory,
        Guid? agentVersionId,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload)
        => new(
            agentKey,
            patternCategory,
            agentVersionId,
            payload.SourceAgentTemplateVersionId,
            payload.PreferredRuntimeAdapterKey!.Trim(),
            payload.PrimaryModelProviderKey!.Trim(),
            payload.PrimaryModelId!.Trim(),
            payload.FallbackModels ?? [],
            payload.PromptTemplateVersionId!.Value,
            payload.OutputSchemaVersionId!.Value,
            payload.QueryIntentVersionId,
            payload.RetrievalStrategyVersionId,
            payload.ReferencedToolDefinitionVersionIds ?? []);

    public static AgentExecutionProfile FromTemplatePayload(
        string agentKey,
        AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument template,
        Guid templateVersionId,
        string? primaryModelProviderKeyOverride = null,
        string? primaryModelIdOverride = null)
    {
        var providerKey = primaryModelProviderKeyOverride
            ?? template.CompositionMetadata?.GetValueOrDefault("primaryModelProviderKey")
            ?? "openai";
        var modelId = primaryModelIdOverride
            ?? template.CompositionMetadata?.GetValueOrDefault("primaryModelId")
            ?? "gpt-4o-mini";

        return new AgentExecutionProfile(
            agentKey,
            template.PatternCategory!.Trim(),
            AgentVersionId: null,
            templateVersionId,
            template.PreferredRuntimeAdapterKey!.Trim(),
            providerKey.Trim(),
            modelId.Trim(),
            [],
            template.PromptTemplateVersionId!.Value,
            template.OutputSchemaVersionId!.Value,
            template.QueryIntentVersionId,
            template.RetrievalStrategyVersionId,
            template.ReferencedToolDefinitionVersionIds ?? []);
    }
}
