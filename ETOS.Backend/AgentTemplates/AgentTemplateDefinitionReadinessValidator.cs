using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AgentTemplates;

public static class AgentTemplateDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(document.TemplateKey))
        {
            notes.Add("templateKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PatternCategory))
        {
            notes.Add("patternCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PatternSummary))
        {
            notes.Add("patternSummary is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PreferredRuntimeAdapterKey))
        {
            notes.Add("preferredRuntimeAdapterKey is required.");
        }
        else if (!AgentRuntimeAdapterKeys.All.Contains(document.PreferredRuntimeAdapterKey, StringComparer.OrdinalIgnoreCase))
        {
            notes.Add($"preferredRuntimeAdapterKey '{document.PreferredRuntimeAdapterKey}' is not a known adapter key.");
        }

        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        var capabilityCount = document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0;

        if (packageCount + ontologyCount == 0)
        {
            notes.Add("At least one compatibleModelPackageVersionId or compatibleOntologyVersionId is required.");
        }

        if (capabilityCount == 0)
        {
            notes.Add("At least one referencedCapabilityDefinitionVersionId is required.");
        }

        if (document.PromptTemplateVersionId is null || document.PromptTemplateVersionId == Guid.Empty)
        {
            notes.Add("promptTemplateVersionId is required.");
        }

        if (document.OutputSchemaVersionId is null || document.OutputSchemaVersionId == Guid.Empty)
        {
            notes.Add("outputSchemaVersionId is required.");
        }

        if (document.QueryIntentVersionId is null || document.QueryIntentVersionId == Guid.Empty)
        {
            notes.Add("queryIntentVersionId is required.");
        }

        if (document.RetrievalStrategyVersionId is null || document.RetrievalStrategyVersionId == Guid.Empty)
        {
            notes.Add("retrievalStrategyVersionId is required.");
        }

        return notes;
    }

    public static async Task<IReadOnlyCollection<string>> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>(ValidateRequiredFields(document));

        foreach (var capabilityVersionId in document.ReferencedCapabilityDefinitionVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                capabilityVersionId,
                CapabilityDefinitionArtifactTypes.CapabilityDefinition,
                "capability definition",
                cancellationToken));
        }

        foreach (var policyVersionId in document.ReferencedBusinessPolicyDefinitionVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                policyVersionId,
                BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition,
                "business policy definition",
                cancellationToken));
        }

        foreach (var optimizationVersionId in document.ReferencedOptimizationModelVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                optimizationVersionId,
                OptimizationModelDefinitionArtifactTypes.OptimizationModel,
                "optimization model",
                cancellationToken));
        }

        foreach (var toolVersionId in document.ReferencedToolDefinitionVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                toolVersionId,
                FutureToolDefinitionArtifactTypes.ToolDefinition,
                "tool definition",
                cancellationToken));
        }

        if (document.PromptTemplateVersionId is Guid promptTemplateVersionId)
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                promptTemplateVersionId,
                "PromptTemplateVersion",
                "prompt template",
                cancellationToken));
        }

        if (document.OutputSchemaVersionId is Guid outputSchemaVersionId)
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                outputSchemaVersionId,
                "OutputSchemaVersion",
                "output schema",
                cancellationToken));
        }

        if (document.QueryIntentVersionId is Guid queryIntentVersionId)
        {
            var intent = await dbContext.QueryIntentVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == queryIntentVersionId, cancellationToken);

            if (intent is null)
            {
                notes.Add($"Query intent version '{queryIntentVersionId}' was not found.");
            }
            else if (intent.TenantId != tenantId)
            {
                notes.Add($"Query intent version '{queryIntentVersionId}' belongs to a different tenant.");
            }
            else if (!intent.IsEnabled)
            {
                notes.Add($"Query intent '{intent.IntentKey}' must be enabled.");
            }
        }

        if (document.RetrievalStrategyVersionId is Guid retrievalStrategyVersionId)
        {
            var strategy = await dbContext.RetrievalStrategyVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == retrievalStrategyVersionId, cancellationToken);

            if (strategy is null)
            {
                notes.Add($"Retrieval strategy version '{retrievalStrategyVersionId}' was not found.");
            }
            else if (strategy.TenantId != tenantId)
            {
                notes.Add($"Retrieval strategy version '{retrievalStrategyVersionId}' belongs to a different tenant.");
            }
            else if (!strategy.IsEnabled)
            {
                notes.Add($"Retrieval strategy '{strategy.StrategyKey}' must be enabled.");
            }
        }

        foreach (var packageId in document.CompatibleModelPackageVersionIds ?? [])
        {
            var package = await dbContext.ModelPackageVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == packageId, cancellationToken);

            if (package is null)
            {
                notes.Add($"Compatible model package '{packageId}' was not found.");
                continue;
            }

            if (package.TenantId != tenantId)
            {
                notes.Add($"Compatible model package '{packageId}' belongs to a different tenant.");
                continue;
            }

            if (package.State != OntologyPublicationState.Published)
            {
                notes.Add($"Compatible model package '{package.Key}' must be published.");
            }
        }

        foreach (var ontologyId in document.CompatibleOntologyVersionIds ?? [])
        {
            var ontology = await dbContext.OntologyVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == ontologyId, cancellationToken);

            if (ontology is null)
            {
                notes.Add($"Compatible ontology '{ontologyId}' was not found.");
                continue;
            }

            if (ontology.TenantId != tenantId)
            {
                notes.Add($"Compatible ontology '{ontologyId}' belongs to a different tenant.");
                continue;
            }

            if (ontology.State != OntologyPublicationState.Published)
            {
                notes.Add($"Compatible ontology '{ontology.Key}' must be published.");
            }
        }

        return notes;
    }

    private static async Task<IReadOnlyCollection<string>> ValidatePublishedArtifactVersionAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid versionId,
        string expectedArtifactType,
        string dependencyLabel,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId, cancellationToken);

        if (version is null)
        {
            notes.Add($"Referenced {dependencyLabel} version '{versionId}' was not found.");
            return notes;
        }

        if (version.TenantId != tenantId)
        {
            notes.Add($"Referenced {dependencyLabel} version '{versionId}' belongs to a different tenant.");
            return notes;
        }

        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == version.ArtifactId, cancellationToken);

        if (artifact is null)
        {
            notes.Add($"Referenced {dependencyLabel} artifact for version '{versionId}' was not found.");
            return notes;
        }

        if (!artifact.ArtifactType.Equals(expectedArtifactType, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add(
                $"Referenced {dependencyLabel} version '{versionId}' belongs to artifact type '{artifact.ArtifactType}' instead of '{expectedArtifactType}'.");
            return notes;
        }

        if (version.ReadinessState != ArtifactReadinessState.Published)
        {
            notes.Add($"Referenced {dependencyLabel} version '{version.VersionLabel}' must be published.");
        }

        return notes;
    }
}
