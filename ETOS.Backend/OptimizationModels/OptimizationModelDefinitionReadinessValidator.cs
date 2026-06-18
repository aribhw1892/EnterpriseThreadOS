using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.OptimizationModels;

public static class OptimizationModelDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        OptimizationModelDefinitionPayloadParser.OptimizationModelDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(document.OptimizationKey))
        {
            notes.Add("optimizationKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ObjectiveCategory))
        {
            notes.Add("objectiveCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ObjectiveSummary))
        {
            notes.Add("objectiveSummary is required.");
        }

        if ((document.InputRequirements?.Count ?? 0) == 0)
        {
            notes.Add("At least one inputRequirements entry is required.");
        }

        var capabilityCount = document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0;
        var policyCount = document.ReferencedBusinessPolicyDefinitionVersionIds?.Count ?? 0;
        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (capabilityCount + policyCount + packageCount + ontologyCount == 0)
        {
            notes.Add(
                "At least one referencedCapabilityDefinitionVersionId, referencedBusinessPolicyDefinitionVersionId, compatibleModelPackageVersionId, or compatibleOntologyVersionId is required.");
        }

        return notes;
    }

    public static async Task<IReadOnlyCollection<string>> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        OptimizationModelDefinitionPayloadParser.OptimizationModelDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>(ValidateRequiredFields(document));

        foreach (var capabilityVersionId in document.ReferencedCapabilityDefinitionVersionIds ?? [])
        {
            var version = await dbContext.ArtifactVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == capabilityVersionId, cancellationToken);

            if (version is null)
            {
                notes.Add($"Referenced capability definition version '{capabilityVersionId}' was not found.");
                continue;
            }

            if (version.TenantId != tenantId)
            {
                notes.Add($"Referenced capability definition version '{capabilityVersionId}' belongs to a different tenant.");
                continue;
            }

            var artifact = await dbContext.Artifacts
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == version.ArtifactId, cancellationToken);

            if (artifact is null)
            {
                notes.Add($"Referenced capability definition artifact for version '{capabilityVersionId}' was not found.");
                continue;
            }

            if (!artifact.ArtifactType.Equals(
                    CapabilityDefinitionArtifactTypes.CapabilityDefinition,
                    StringComparison.OrdinalIgnoreCase))
            {
                notes.Add(
                    $"Referenced version '{capabilityVersionId}' belongs to artifact type '{artifact.ArtifactType}' instead of '{CapabilityDefinitionArtifactTypes.CapabilityDefinition}'.");
                continue;
            }

            if (version.ReadinessState != ArtifactReadinessState.Published)
            {
                notes.Add($"Referenced capability definition version '{version.VersionLabel}' must be published.");
            }
        }

        foreach (var policyVersionId in document.ReferencedBusinessPolicyDefinitionVersionIds ?? [])
        {
            var version = await dbContext.ArtifactVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == policyVersionId, cancellationToken);

            if (version is null)
            {
                notes.Add($"Referenced business policy definition version '{policyVersionId}' was not found.");
                continue;
            }

            if (version.TenantId != tenantId)
            {
                notes.Add($"Referenced business policy definition version '{policyVersionId}' belongs to a different tenant.");
                continue;
            }

            var artifact = await dbContext.Artifacts
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == version.ArtifactId, cancellationToken);

            if (artifact is null)
            {
                notes.Add($"Referenced business policy definition artifact for version '{policyVersionId}' was not found.");
                continue;
            }

            if (!artifact.ArtifactType.Equals(
                    BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition,
                    StringComparison.OrdinalIgnoreCase))
            {
                notes.Add(
                    $"Referenced version '{policyVersionId}' belongs to artifact type '{artifact.ArtifactType}' instead of '{BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition}'.");
                continue;
            }

            if (version.ReadinessState != ArtifactReadinessState.Published)
            {
                notes.Add($"Referenced business policy definition version '{version.VersionLabel}' must be published.");
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
}
