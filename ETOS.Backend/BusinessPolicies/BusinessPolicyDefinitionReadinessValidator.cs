using ETOS.Backend.Artifacts;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.BusinessPolicies;

public static class BusinessPolicyDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        BusinessPolicyDefinitionPayloadParser.BusinessPolicyDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(document.PolicyKey))
        {
            notes.Add("policyKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ConstraintCategory))
        {
            notes.Add("constraintCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ConstraintSummary))
        {
            notes.Add("constraintSummary is required.");
        }

        var capabilityCount = document.ReferencedCapabilityDefinitionVersionIds?.Count ?? 0;
        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (capabilityCount + packageCount + ontologyCount == 0)
        {
            notes.Add(
                "At least one referencedCapabilityDefinitionVersionId, compatibleModelPackageVersionId, or compatibleOntologyVersionId is required.");
        }

        if ((document.ConstraintRules?.Count ?? 0) == 0)
        {
            notes.Add("At least one constraintRules entry is required.");
        }

        return notes;
    }

    public static async Task<IReadOnlyCollection<string>> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        BusinessPolicyDefinitionPayloadParser.BusinessPolicyDefinitionPayloadDocument document,
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
