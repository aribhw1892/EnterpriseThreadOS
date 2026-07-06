using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Learning;

public static class LearningDevelopmentSeeder
{
    public static async Task SeedPlaceholderArtifactsAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        await SeedPlaceholderAsync(
            dbContext,
            tenantId,
            ownerUserId,
            LearningArtifactTypes.LearningPolicy,
            "Default Learning Policy Placeholder",
            cancellationToken);
        await SeedPlaceholderAsync(
            dbContext,
            tenantId,
            ownerUserId,
            LearningArtifactTypes.LearningModel,
            "Default Learning Model Placeholder",
            cancellationToken);
    }

    private static async Task SeedPlaceholderAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid ownerUserId,
        string artifactType,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedType = artifactType.ToUpperInvariant();
        var exists = await dbContext.Artifacts.AnyAsync(
            artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedType,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new { status = "placeholder", execution = "deferred" });
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactType = artifactType,
            NormalizedArtifactType = normalizedType,
            Name = name,
            Description = "Issue 20 placeholder artifact for future learning governance.",
            OwnerUserId = ownerUserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "0.1.0",
            NormalizedVersionLabel = "0.1.0",
            Summary = "placeholder",
            PayloadJson = payload,
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
