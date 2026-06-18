using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Capabilities;

public static class CapabilityDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        CapabilityDefinitionPayloadParser.CapabilityDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(document.CapabilityKey))
        {
            notes.Add("capabilityKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.OutcomeCategory))
        {
            notes.Add("outcomeCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(document.OutcomeSummary))
        {
            notes.Add("outcomeSummary is required.");
        }

        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (packageCount + ontologyCount == 0)
        {
            notes.Add("At least one compatibleModelPackageVersionId or compatibleOntologyVersionId is required.");
        }

        return notes;
    }

    public static async Task<IReadOnlyCollection<string>> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        CapabilityDefinitionPayloadParser.CapabilityDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>(ValidateRequiredFields(document));

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
