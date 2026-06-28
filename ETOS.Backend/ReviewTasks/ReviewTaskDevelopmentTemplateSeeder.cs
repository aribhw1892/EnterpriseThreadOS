using ETOS.Backend.Artifacts;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ReviewTasks;

public static class ReviewTaskDevelopmentTemplateSeeder
{
    public static async Task SeedPublishedTemplatesAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var normalizedType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant();
        var exists = await dbContext.Artifacts.AnyAsync(
            artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedType,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var seeds = new[]
        {
            ("Data Quality Review Template", "data-quality-review", "data-quality-review", false, false),
            ("Business Action Review Template", "business-action-review", "business-action-review", true, true),
            ("Governance Security Review Template", "governance-security-review", "governance-security-review", false, true),
            ("Access Request Review Template", "access-request-review", "access-request-review", false, false)
        };

        foreach (var (name, templateKey, reviewTaskType, requiresDq, escalationEnabled) in seeds)
        {
            var payload = ReviewTaskTemplatePayloadParser.Create(
                templateKey,
                reviewTaskType,
                null,
                requiresDq,
                new ReviewTaskTemplatePayloadParser.ReviewTaskTemplateEscalationPathDocument
                {
                    Enabled = escalationEnabled,
                    EscalationTargetRoleKey = escalationEnabled ? "tenant-admin" : null,
                    EscalationPolicyId = escalationEnabled ? "governance-escalation-v1" : null,
                    SlaPolicyVersion = escalationEnabled ? "placeholder-sla-v1" : null
                },
                new Dictionary<string, string> { ["primaryOwner"] = "tenant-admin" },
                ["accept", "reject", "defer"]);

            var artifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate,
                NormalizedArtifactType = normalizedType,
                Name = name,
                Description = $"Development seed template for {templateKey}.",
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
                Summary = reviewTaskType,
                PayloadJson = ReviewTaskTemplatePayloadParser.Serialize(payload),
                ReadinessState = ArtifactReadinessState.Published,
                CreatedByUserId = ownerUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Artifacts.Add(artifact);
            dbContext.ArtifactVersions.Add(version);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
