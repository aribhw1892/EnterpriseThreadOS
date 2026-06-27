using System.Text.Json;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Agents;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.AgentRuntime;

public interface IAgentExecutionProfileResolver
{
    Task<AgentExecutionProfile> ResolveByAgentKeyAsync(
        Guid tenantId,
        string agentKey,
        CancellationToken cancellationToken);

    Task<AgentExecutionProfile> ResolveByAgentVersionIdAsync(
        Guid tenantId,
        Guid agentVersionId,
        CancellationToken cancellationToken);

    Task<AgentExecutionProfile> ResolveMappingAssistantAsync(
        Guid tenantId,
        ResolvedModelPackageContext modelContext,
        string? agentKeyOverride = null,
        Guid? agentVersionIdOverride = null,
        CancellationToken cancellationToken = default);
}

public sealed class AgentExecutionProfileResolver(
    EnterpriseThreadDbContext dbContext,
    IOptions<MappingSuggestionOptions> mappingOptions) : IAgentExecutionProfileResolver
{
    private const string MappingAssistantPatternCategory = "mapping-assistant";
    private const string DefaultMappingAssistantAgentKey = "import-mapping-assistant";

    public async Task<AgentExecutionProfile> ResolveByAgentKeyAsync(
        Guid tenantId,
        string agentKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            throw new RequestValidationException("agentKey is required.");
        }

        var normalizedAgentKey = agentKey.Trim();
        var publishedAgent = await TryResolvePublishedAgentByKeyAsync(tenantId, normalizedAgentKey, cancellationToken);
        if (publishedAgent is not null)
        {
            return publishedAgent;
        }

        var templateProfile = await TryResolvePublishedTemplateByKeyAsync(tenantId, normalizedAgentKey, cancellationToken);
        if (templateProfile is not null)
        {
            return templateProfile;
        }

        throw new RequestValidationException(
            $"No published mapping assistant agent or template was found for key '{normalizedAgentKey}'. " +
            "Install the reference package or publish an agent at /agents.");
    }

    public async Task<AgentExecutionProfile> ResolveByAgentVersionIdAsync(
        Guid tenantId,
        Guid agentVersionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == agentVersionId && item.TenantId == tenantId, cancellationToken)
            ?? throw new RequestValidationException($"Agent version '{agentVersionId}' was not found.");

        if (!version.Artifact!.ArtifactType.Equals(AgentDefinitionArtifactTypes.AgentVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not an agent version.");
        }

        if (version.ReadinessState != ArtifactReadinessState.Published)
        {
            throw new RequestValidationException("Agent version must be published.");
        }

        var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var patternCategory = await ResolvePatternCategoryAsync(tenantId, payload, cancellationToken);
        return AgentExecutionProfile.FromAgentPayload(payload.AgentKey!.Trim(), patternCategory, version.Id, payload);
    }

    public async Task<AgentExecutionProfile> ResolveMappingAssistantAsync(
        Guid tenantId,
        ResolvedModelPackageContext modelContext,
        string? agentKeyOverride = null,
        Guid? agentVersionIdOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (agentVersionIdOverride is Guid pinnedVersionId)
        {
            return await ResolveByAgentVersionIdAsync(tenantId, pinnedVersionId, cancellationToken);
        }

        var agentKey = agentKeyOverride
            ?? modelContext.ImportProfile.MappingAssistantAgentKey
            ?? mappingOptions.Value.MappingAssistantAgentKey
            ?? DefaultMappingAssistantAgentKey;

        return await ResolveByAgentKeyAsync(tenantId, agentKey, cancellationToken);
    }

    private async Task<AgentExecutionProfile?> TryResolvePublishedAgentByKeyAsync(
        Guid tenantId,
        string agentKey,
        CancellationToken cancellationToken)
    {
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentDefinitionArtifactTypes.AgentVersion.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (!string.Equals(payload.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var patternCategory = await ResolvePatternCategoryAsync(tenantId, payload, cancellationToken);
            return AgentExecutionProfile.FromAgentPayload(agentKey, patternCategory, version.Id, payload);
        }

        return null;
    }

    private async Task<AgentExecutionProfile?> TryResolvePublishedTemplateByKeyAsync(
        Guid tenantId,
        string templateKey,
        CancellationToken cancellationToken)
    {
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.NormalizedArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant())
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            var template = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (!string.Equals(template.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return AgentExecutionProfile.FromTemplatePayload(templateKey, template, version.Id);
        }

        return null;
    }

    private async Task<string> ResolvePatternCategoryAsync(
        Guid tenantId,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        if (payload.SourceAgentTemplateVersionId is Guid templateVersionId)
        {
            var templateVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == templateVersionId && item.TenantId == tenantId, cancellationToken);
            if (templateVersion?.PayloadJson is not null)
            {
                var template = AgentTemplateDefinitionPayloadParser.Deserialize(templateVersion.PayloadJson);
                if (!string.IsNullOrWhiteSpace(template.PatternCategory))
                {
                    return template.PatternCategory.Trim();
                }
            }
        }

        return MappingAssistantPatternCategory;
    }
}
