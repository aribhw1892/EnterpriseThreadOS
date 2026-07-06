using System.Text.Json;
using ETOS.Backend.Agents;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Workflows;

public static class WorkflowDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        try
        {
            WorkflowDefinitionPayloadParser.ValidateCore(document);
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
        }

        notes.AddRange(WorkflowDefinitionPayloadParser.ValidateWorkflowDefinitionJson(document.WorkflowDefinitionJson));

        if (document.TriggerConfig?.Manual?.Enabled != true)
        {
            notes.Add("Manual trigger must be enabled for MVP workflow versions.");
        }

        return notes;
    }

    public static async Task<(IReadOnlyCollection<string> Notes, WorkflowDefinitionPayloadParser.DerivedCapabilityRiskDocument? DerivedRisk)> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>(ValidateRequiredFields(document));

        foreach (var agentVersionId in document.ReferencedAgentVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                agentVersionId,
                AgentDefinitionArtifactTypes.AgentVersion,
                "agent version",
                cancellationToken));
            notes.AddRange(await ValidateAgentReadOnlyConstraintsAsync(
                dbContext,
                tenantId,
                agentVersionId,
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
            notes.AddRange(await ValidateToolReadOnlyConstraintsAsync(
                dbContext,
                tenantId,
                toolVersionId,
                cancellationToken));
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

        var steps = string.IsNullOrWhiteSpace(document.WorkflowDefinitionJson)
            ? []
            : WorkflowDefinitionPayloadParser.DeserializeWorkflowDefinitionJson(document.WorkflowDefinitionJson);

        foreach (var step in steps)
        {
            switch (step.StepType?.Trim())
            {
                case WorkflowStepTypes.AgentExecute when step.AgentVersionId is Guid agentVersionId:
                    notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                        dbContext,
                        tenantId,
                        agentVersionId,
                        AgentDefinitionArtifactTypes.AgentVersion,
                        $"agent version for step '{step.StepKey}'",
                        cancellationToken));
                    notes.AddRange(await ValidateAgentReadOnlyConstraintsAsync(
                        dbContext,
                        tenantId,
                        agentVersionId,
                        cancellationToken));
                    break;
                case WorkflowStepTypes.ToolExecute when step.ToolDefinitionVersionId is Guid toolVersionId:
                    notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                        dbContext,
                        tenantId,
                        toolVersionId,
                        ToolDefinitionArtifactTypes.ToolDefinition,
                        $"tool definition for step '{step.StepKey}'",
                        cancellationToken));
                    notes.AddRange(await ValidateToolReadOnlyConstraintsAsync(
                        dbContext,
                        tenantId,
                        toolVersionId,
                        cancellationToken));
                    break;
                case WorkflowStepTypes.BusinessPolicyCheck when step.BusinessPolicyDefinitionVersionId is Guid policyVersionId:
                    notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                        dbContext,
                        tenantId,
                        policyVersionId,
                        BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition,
                        $"business policy for step '{step.StepKey}'",
                        cancellationToken));
                    break;
                case WorkflowStepTypes.OptimizationEvaluate when step.OptimizationModelVersionId is Guid optimizationVersionId:
                    notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                        dbContext,
                        tenantId,
                        optimizationVersionId,
                        OptimizationModelDefinitionArtifactTypes.OptimizationModel,
                        $"optimization model for step '{step.StepKey}'",
                        cancellationToken));
                    break;
                case WorkflowStepTypes.CreateReviewTask when step.ReviewTaskTemplateVersionId is Guid templateVersionId:
                    notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                        dbContext,
                        tenantId,
                        templateVersionId,
                        "ReviewTaskTemplateVersion",
                        $"review task template for step '{step.StepKey}'",
                        cancellationToken));
                    break;
            }
        }

        if (notes.Count > 0)
        {
            return (notes, null);
        }

        var toolVersionIds = CollectToolVersionIds(document, steps);
        var toolRiskContributions = await LoadToolRiskContributionsAsync(
            dbContext,
            tenantId,
            toolVersionIds,
            cancellationToken);

        var agentVersionIds = CollectAgentVersionIds(document, steps);
        var agentRiskLevels = await LoadAgentEffectiveRiskLevelsAsync(
            dbContext,
            tenantId,
            agentVersionIds,
            cancellationToken);

        var effectiveRiskLevel = ComputeEffectiveRiskLevel(toolRiskContributions, agentRiskLevels);
        var permissionCeiling = ResolvePermissionCeiling(document.WorkflowScope ?? WorkflowScopes.Tenant);

        if (CompareRiskLevels(effectiveRiskLevel, permissionCeiling) > 0)
        {
            notes.Add(
                $"Derived effective risk level '{effectiveRiskLevel}' exceeds workflow permission ceiling '{permissionCeiling}'.");
        }

        var derivedRisk = new WorkflowDefinitionPayloadParser.DerivedCapabilityRiskDocument
        {
            EffectiveRiskLevel = effectiveRiskLevel,
            ToolRiskContributions = toolRiskContributions,
            PermissionCeiling = permissionCeiling
        };

        return (notes, derivedRisk);
    }

    private static HashSet<Guid> CollectToolVersionIds(
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument document,
        IReadOnlyCollection<WorkflowDefinitionPayloadParser.WorkflowStepDocument> steps)
    {
        var ids = new HashSet<Guid>(document.ReferencedToolDefinitionVersionIds ?? []);
        foreach (var step in steps)
        {
            if (step.ToolDefinitionVersionId is Guid toolVersionId)
            {
                ids.Add(toolVersionId);
            }
        }

        return ids;
    }

    private static HashSet<Guid> CollectAgentVersionIds(
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument document,
        IReadOnlyCollection<WorkflowDefinitionPayloadParser.WorkflowStepDocument> steps)
    {
        var ids = new HashSet<Guid>(document.ReferencedAgentVersionIds ?? []);
        foreach (var step in steps)
        {
            if (step.AgentVersionId is Guid agentVersionId)
            {
                ids.Add(agentVersionId);
            }
        }

        return ids;
    }

    private static async Task<IReadOnlyCollection<string>> ValidateAgentReadOnlyConstraintsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid agentVersionId,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var payloadJson = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(item => item.Id == agentVersionId && item.TenantId == tenantId)
            .Select(item => item.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return notes;
        }

        try
        {
            var agentPayload = AgentDefinitionPayloadParser.Deserialize(payloadJson);
            foreach (var toolVersionId in agentPayload.ReferencedToolDefinitionVersionIds ?? [])
            {
                notes.AddRange(await ValidateToolReadOnlyConstraintsAsync(
                    dbContext,
                    tenantId,
                    toolVersionId,
                    cancellationToken));
            }

            if (agentPayload.OutputSchemaVersionId is Guid outputSchemaVersionId
                && OutputSchemaCreatesDecision(await LoadPayloadJsonAsync(dbContext, outputSchemaVersionId, cancellationToken)))
            {
                notes.Add($"Referenced agent '{agentVersionId}' output schema createsDecision must be false for workflows.");
            }
        }
        catch (RequestValidationException)
        {
            notes.Add($"Referenced agent version '{agentVersionId}' payload is invalid.");
        }

        return notes;
    }

    private static async Task<IReadOnlyCollection<string>> ValidateToolReadOnlyConstraintsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid toolVersionId,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var payloadJson = await LoadPayloadJsonAsync(dbContext, toolVersionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return notes;
        }

        try
        {
            var toolPayload = ToolDefinitionPayloadParser.Deserialize(payloadJson);
            if (toolPayload.CreatesDecision)
            {
                notes.Add($"Referenced tool version '{toolVersionId}' createsDecision must be false for workflows.");
            }

            if (toolPayload.WritesExternalSystem)
            {
                notes.Add($"Referenced tool version '{toolVersionId}' writesExternalSystem must be false for workflows.");
            }
        }
        catch (RequestValidationException)
        {
            notes.Add($"Referenced tool version '{toolVersionId}' payload is invalid.");
        }

        return notes;
    }

    private static async Task<string?> LoadPayloadJsonAsync(
        EnterpriseThreadDbContext dbContext,
        Guid versionId,
        CancellationToken cancellationToken)
        => await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(item => item.Id == versionId)
            .Select(item => item.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);

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

    private static async Task<List<WorkflowDefinitionPayloadParser.ToolRiskContributionDocument>> LoadToolRiskContributionsAsync(
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

            return new WorkflowDefinitionPayloadParser.ToolRiskContributionDocument
            {
                ToolDefinitionVersionId = version.Id,
                RiskLevel = riskLevel
            };
        }).ToList();
    }

    private static async Task<IReadOnlyCollection<string>> LoadAgentEffectiveRiskLevelsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        IReadOnlyCollection<Guid> agentVersionIds,
        CancellationToken cancellationToken)
    {
        if (agentVersionIds.Count == 0)
        {
            return [];
        }

        var payloads = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && agentVersionIds.Contains(item.Id))
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);

        var riskLevels = new List<string>();
        foreach (var payloadJson in payloads)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                continue;
            }

            try
            {
                var agentPayload = AgentDefinitionPayloadParser.Deserialize(payloadJson);
                var derivedRisk = agentPayload.DerivedCapabilityRiskJson?.EffectiveRiskLevel;
                if (!string.IsNullOrWhiteSpace(derivedRisk))
                {
                    riskLevels.Add(derivedRisk);
                }
            }
            catch (RequestValidationException)
            {
                // Ignore invalid agent payloads here; publish validation already blocks them.
            }
        }

        return riskLevels;
    }

    private static string ComputeEffectiveRiskLevel(
        IReadOnlyCollection<WorkflowDefinitionPayloadParser.ToolRiskContributionDocument> toolRiskContributions,
        IReadOnlyCollection<string> agentRiskLevels)
    {
        var maxRisk = ToolRiskLevels.Low;
        foreach (var contribution in toolRiskContributions)
        {
            maxRisk = MaxRiskLevel(maxRisk, contribution.RiskLevel ?? ToolRiskLevels.Low);
        }

        foreach (var agentRisk in agentRiskLevels)
        {
            maxRisk = MaxRiskLevel(maxRisk, agentRisk);
        }

        return maxRisk;
    }

    private static string ResolvePermissionCeiling(string workflowScope)
        => workflowScope.Equals(WorkflowScopes.Platform, StringComparison.OrdinalIgnoreCase)
            ? ToolRiskLevels.High
            : workflowScope.Equals(WorkflowScopes.Personal, StringComparison.OrdinalIgnoreCase)
                ? ToolRiskLevels.Low
                : ToolRiskLevels.Medium;

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
}
