using ETOS.Backend.Artifacts;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Outcomes;

public static class OutcomeTaxonomyDevelopmentSeeder
{
    public static readonly string DefaultTaxonomyKey = "platform-governance-outcomes-v1";

    public static readonly IReadOnlyCollection<string> DefaultCategories =
    [
        "approved",
        "rejected",
        "no_action",
        "defer",
        "duplicate",
        "known_exception",
        "escalated"
    ];

    public static async Task<OutcomeDevelopmentSeedResult?> SeedPublishedTaxonomyAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var normalizedType = OutcomeTaxonomyArtifactTypes.OutcomeTaxonomy.ToUpperInvariant();
        var existingVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Join(
                dbContext.Artifacts.Where(artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedType),
                version => version.ArtifactId,
                artifact => artifact.Id,
                (version, artifact) => version)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingVersion is not null)
        {
            return new OutcomeDevelopmentSeedResult(existingVersion.ArtifactId, existingVersion.Id);
        }

        var payload = OutcomeTaxonomyPayloadParser.Create(DefaultTaxonomyKey, DefaultCategories);
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactType = OutcomeTaxonomyArtifactTypes.OutcomeTaxonomy,
            NormalizedArtifactType = normalizedType,
            Name = "Platform Governance Outcome Taxonomy",
            Description = "Development seed outcome taxonomy for Issue 20.",
            OwnerUserId = ownerUserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = DefaultTaxonomyKey,
            PayloadJson = OutcomeTaxonomyPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new OutcomeDevelopmentSeedResult(artifact.Id, version.Id);
    }
}
