using ETOS.Backend.Artifacts;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public interface IPublishedToolVersionResolver
{
    Task<(Guid ArtifactId, Guid VersionId)?> TryResolvePublishedToolAsync(
        Guid tenantId,
        string toolKey,
        CancellationToken cancellationToken);
}

public sealed class PublishedToolVersionResolver(EnterpriseThreadDbContext dbContext) : IPublishedToolVersionResolver
{
    public async Task<(Guid ArtifactId, Guid VersionId)?> TryResolvePublishedToolAsync(
        Guid tenantId,
        string toolKey,
        CancellationToken cancellationToken)
    {
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition)
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.ToolKey, toolKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        return null;
    }
}
