using System.Text.Json;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Agents;

public static class AgentDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        try
        {
            AgentDefinitionPayloadParser.ValidateCore(document);
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
        }

        if (AgentMvpBlockedRuntimeAdapters.All.Contains(
                document.PreferredRuntimeAdapterKey ?? string.Empty,
                StringComparer.OrdinalIgnoreCase))
        {
            notes.Add(
                $"preferredRuntimeAdapterKey '{document.PreferredRuntimeAdapterKey}' is deferred for MVP agent execution.");
        }

        return notes;
    }

    public static async Task<(IReadOnlyCollection<string> Notes, AgentDefinitionPayloadParser.DerivedCapabilityRiskDocument? DerivedRisk)> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument document,
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
                ToolDefinitionArtifactTypes.ToolDefinition,
                "tool definition",
                cancellationToken));
        }

        foreach (var skillVersionId in document.ReferencedSkillDefinitionVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                skillVersionId,
                SkillDefinitionArtifactTypes.SkillDefinition,
                "skill definition",
                cancellationToken));
        }

        notes.AddRange(await ValidatePublishedArtifactVersionAsync(
            dbContext,
            tenantId,
            document.AgentTypeDefinitionVersionId,
            AgentTypeDefinitionArtifactTypes.AgentTypeDefinition,
            "agent type definition",
            cancellationToken));

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
            notes.AddRange(await ValidateOutputSchemaAsync(
                dbContext,
                tenantId,
                outputSchemaVersionId,
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

        RetrievalStrategyVersionSnapshot? retrievalStrategy = null;
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
            else
            {
                retrievalStrategy = new RetrievalStrategyVersionSnapshot(
                    strategy.AllowsSemanticFallback,
                    strategy.AllowsVectorFallback);
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

        if (document.SourceAgentTemplateVersionId is Guid sourceTemplateVersionId)
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                sourceTemplateVersionId,
                AgentTemplateDefinitionArtifactTypes.AgentTemplate,
                "source agent template",
                cancellationToken));
        }

        if (notes.Count > 0)
        {
            return (notes, null);
        }

        var toolRiskContributions = await LoadToolRiskContributionsAsync(
            dbContext,
            tenantId,
            document.ReferencedToolDefinitionVersionIds ?? [],
            cancellationToken);

        var agentTypeRiskBaseline = await LoadAgentTypeRiskBaselineAsync(
            dbContext,
            tenantId,
            document.AgentTypeDefinitionVersionId,
            cancellationToken);

        var effectiveRiskLevel = ComputeEffectiveRiskLevel(toolRiskContributions, retrievalStrategy);
        var permissionCeiling = agentTypeRiskBaseline ?? effectiveRiskLevel;

        if (agentTypeRiskBaseline is not null
            && CompareRiskLevels(effectiveRiskLevel, agentTypeRiskBaseline) > 0)
        {
            notes.Add(
                $"Derived effective risk level '{effectiveRiskLevel}' exceeds agent type risk baseline '{agentTypeRiskBaseline}'.");
        }

        var derivedRisk = new AgentDefinitionPayloadParser.DerivedCapabilityRiskDocument
        {
            EffectiveRiskLevel = effectiveRiskLevel,
            ToolRiskContributions = toolRiskContributions,
            RetrievalRisk = new AgentDefinitionPayloadParser.RetrievalRiskDocument
            {
                AllowsSemanticFallback = retrievalStrategy?.AllowsSemanticFallback ?? false,
                AllowsVectorFallback = retrievalStrategy?.AllowsVectorFallback ?? false
            },
            PermissionCeiling = permissionCeiling
        };

        return (notes, derivedRisk);
    }

    private static async Task<IReadOnlyCollection<string>> ValidateOutputSchemaAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid outputSchemaVersionId,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == outputSchemaVersionId, cancellationToken);

        if (version is null)
        {
            notes.Add($"Referenced output schema version '{outputSchemaVersionId}' was not found.");
            return notes;
        }

        if (version.TenantId != tenantId)
        {
            notes.Add($"Referenced output schema version '{outputSchemaVersionId}' belongs to a different tenant.");
            return notes;
        }

        if (!version.Artifact!.ArtifactType.Equals("OutputSchemaVersion", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"Referenced artifact '{outputSchemaVersionId}' is not an OutputSchemaVersion.");
            return notes;
        }

        if (version.ReadinessState != ArtifactReadinessState.Published)
        {
            notes.Add($"Referenced output schema version '{version.VersionLabel}' must be published.");
            return notes;
        }

        if (OutputSchemaCreatesDecision(version.PayloadJson))
        {
            notes.Add("Referenced output schema createsDecision must be false for agent versions.");
        }

        return notes;
    }

    private static bool OutputSchemaCreatesDecision(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static async Task<List<AgentDefinitionPayloadParser.ToolRiskContributionDocument>> LoadToolRiskContributionsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        IReadOnlyCollection<Guid> toolVersionIds,
        CancellationToken cancellationToken)
    {
        if (toolVersionIds.Count == 0)
        {
            return [];
        }

        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && toolVersionIds.Contains(item.Id))
            .Select(item => new { item.Id, item.PayloadJson })
            .ToListAsync(cancellationToken);

        return versions.Select(version =>
        {
            var riskLevel = ToolRiskLevels.Low;
            if (!string.IsNullOrWhiteSpace(version.PayloadJson))
            {
                try
                {
                    var toolPayload = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson);
                    riskLevel = toolPayload.RiskLevel ?? ToolRiskLevels.Low;
                }
                catch (RequestValidationException)
                {
                    riskLevel = ToolRiskLevels.Low;
                }
            }

            return new AgentDefinitionPayloadParser.ToolRiskContributionDocument
            {
                ToolDefinitionVersionId = version.Id,
                RiskLevel = riskLevel
            };
        }).ToList();
    }

    private static async Task<string?> LoadAgentTypeRiskBaselineAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid agentTypeVersionId,
        CancellationToken cancellationToken)
    {
        var payloadJson = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(item => item.Id == agentTypeVersionId && item.TenantId == tenantId)
            .Select(item => item.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return AgentTypeDefinitionPayloadParser.Deserialize(payloadJson).RiskBaseline;
        }
        catch (RequestValidationException)
        {
            return null;
        }
    }

    private static string ComputeEffectiveRiskLevel(
        IReadOnlyCollection<AgentDefinitionPayloadParser.ToolRiskContributionDocument> toolRiskContributions,
        RetrievalStrategyVersionSnapshot? retrievalStrategy)
    {
        var maxRisk = ToolRiskLevels.Low;
        foreach (var contribution in toolRiskContributions)
        {
            maxRisk = MaxRiskLevel(maxRisk, contribution.RiskLevel ?? ToolRiskLevels.Low);
        }

        if (retrievalStrategy?.AllowsSemanticFallback == true || retrievalStrategy?.AllowsVectorFallback == true)
        {
            maxRisk = MaxRiskLevel(maxRisk, ToolRiskLevels.Medium);
        }

        return maxRisk;
    }

    private static string MaxRiskLevel(string left, string right)
        => CompareRiskLevels(left, right) >= 0 ? NormalizeRiskLevel(left) : NormalizeRiskLevel(right);

    private static string NormalizeRiskLevel(string riskLevel)
    {
        if (riskLevel.Equals(ToolRiskLevels.High, StringComparison.OrdinalIgnoreCase))
        {
            return ToolRiskLevels.High;
        }

        if (riskLevel.Equals(ToolRiskLevels.Medium, StringComparison.OrdinalIgnoreCase))
        {
            return ToolRiskLevels.Medium;
        }

        return ToolRiskLevels.Low;
    }

    private static int CompareRiskLevels(string left, string right)
        => RiskLevelRank(NormalizeRiskLevel(left)).CompareTo(RiskLevelRank(NormalizeRiskLevel(right)));

    private static int RiskLevelRank(string riskLevel)
        => riskLevel switch
        {
            var value when value.Equals(ToolRiskLevels.High, StringComparison.OrdinalIgnoreCase) => 2,
            var value when value.Equals(ToolRiskLevels.Medium, StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };

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

    private sealed record RetrievalStrategyVersionSnapshot(
        bool AllowsSemanticFallback,
        bool AllowsVectorFallback);
}
