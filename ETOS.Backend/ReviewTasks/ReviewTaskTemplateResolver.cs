using ETOS.Backend.Artifacts;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ReviewTasks;

public interface IReviewTaskTemplateResolver
{
    Task<(Guid ArtifactId, Guid VersionId, ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument Template)?> ResolvePublishedTemplateAsync(
        Guid tenantId,
        ReviewTaskSourceType sourceType,
        string? requiredReviewPath,
        RecommendationType? recommendationType,
        CancellationToken cancellationToken);
}

public sealed class ReviewTaskTemplateResolver(EnterpriseThreadDbContext dbContext) : IReviewTaskTemplateResolver
{
    public async Task<(Guid ArtifactId, Guid VersionId, ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument Template)?> ResolvePublishedTemplateAsync(
        Guid tenantId,
        ReviewTaskSourceType sourceType,
        string? requiredReviewPath,
        RecommendationType? recommendationType,
        CancellationToken cancellationToken)
    {
        var templateKey = ResolveTemplateKey(sourceType, requiredReviewPath, recommendationType);
        var normalizedType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant();

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.NormalizedArtifactType == normalizedType)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (artifacts.Count == 0)
        {
            return null;
        }

        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifacts.Contains(version.ArtifactId)
                && version.TenantId == tenantId
                && version.ReadinessState == ArtifactReadinessState.Published)
            .OrderByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            if (string.IsNullOrWhiteSpace(version.PayloadJson))
            {
                continue;
            }

            var payload = ReviewTaskTemplatePayloadParser.Deserialize(version.PayloadJson);
            if (payload.TemplateKey.Equals(templateKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id, payload);
            }
        }

        return null;
    }

    public static string ResolveTemplateKey(
        ReviewTaskSourceType sourceType,
        string? requiredReviewPath,
        RecommendationType? recommendationType)
    {
        if (!string.IsNullOrWhiteSpace(requiredReviewPath))
        {
            var normalizedPath = requiredReviewPath.Trim().ToUpperInvariant();
            if (normalizedPath.Contains("ACCESS", StringComparison.Ordinal))
            {
                return "access-request-review";
            }

            if (normalizedPath.Contains("SECURITY", StringComparison.Ordinal) || normalizedPath.Contains("GOVERNANCE", StringComparison.Ordinal))
            {
                return "governance-security-review";
            }

            if (normalizedPath.Contains("DATA", StringComparison.Ordinal) || normalizedPath.Contains("QUALITY", StringComparison.Ordinal))
            {
                return "data-quality-review";
            }
        }

        return sourceType switch
        {
            ReviewTaskSourceType.DataQuality => "data-quality-review",
            ReviewTaskSourceType.SecurityEvent => "governance-security-review",
            ReviewTaskSourceType.AccessRequest => "access-request-review",
            ReviewTaskSourceType.Recommendation when recommendationType == RecommendationType.Security => "governance-security-review",
            ReviewTaskSourceType.Recommendation when recommendationType == RecommendationType.DataQuality => "data-quality-review",
            _ => "business-action-review"
        };
    }
}
