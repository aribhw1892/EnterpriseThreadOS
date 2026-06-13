using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Recommendations;

public interface IRecommendationEvidenceResolver
{
    Task<bool> HasExcludedFromTrustedRecommendationsAsync(
        Guid tenantId,
        IReadOnlyCollection<RecommendationEvidenceLinkResponse> evidenceLinks,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RecommendationEvidenceLinkResponse>> EnrichEvidenceLinksAsync(
        Guid tenantId,
        IReadOnlyCollection<RecommendationEvidenceLinkResponse> evidenceLinks,
        CancellationToken cancellationToken);
}

public sealed class RecommendationEvidenceResolver(EnterpriseThreadDbContext dbContext) : IRecommendationEvidenceResolver
{
    public async Task<bool> HasExcludedFromTrustedRecommendationsAsync(
        Guid tenantId,
        IReadOnlyCollection<RecommendationEvidenceLinkResponse> evidenceLinks,
        CancellationToken cancellationToken)
    {
        foreach (var link in evidenceLinks)
        {
            if (link.TrustState == TrustState.Conflicted)
            {
                return true;
            }

            switch (link.EvidenceType)
            {
                case EvidenceLinkType.DataQualityIssue:
                    var issue = await dbContext.DataQualityIssues
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.Id == link.SourceId && item.TenantId == tenantId, cancellationToken);
                    if (issue?.ExcludedFromTrustedRecommendations == true)
                    {
                        return true;
                    }

                    break;
                case EvidenceLinkType.BomComparisonRun:
                    var run = await dbContext.BomComparisonRuns
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.Id == link.SourceId && item.TenantId == tenantId, cancellationToken);
                    if (run?.UnresolvedIdentityCount > 0)
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    public async Task<IReadOnlyCollection<RecommendationEvidenceLinkResponse>> EnrichEvidenceLinksAsync(
        Guid tenantId,
        IReadOnlyCollection<RecommendationEvidenceLinkResponse> evidenceLinks,
        CancellationToken cancellationToken)
    {
        var enriched = new List<RecommendationEvidenceLinkResponse>();

        foreach (var link in evidenceLinks)
        {
            enriched.Add(await EnrichLinkAsync(tenantId, link, cancellationToken));
        }

        return enriched;
    }

    private async Task<RecommendationEvidenceLinkResponse> EnrichLinkAsync(
        Guid tenantId,
        RecommendationEvidenceLinkResponse link,
        CancellationToken cancellationToken)
    {
        switch (link.EvidenceType)
        {
            case EvidenceLinkType.DataQualityIssue:
                var issue = await dbContext.DataQualityIssues
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == link.SourceId && item.TenantId == tenantId, cancellationToken);
                if (issue is null)
                {
                    throw new RequestValidationException($"Data quality issue '{link.SourceId}' was not found.");
                }

                return link with
                {
                    SafeSummary = string.IsNullOrWhiteSpace(link.SafeSummary) ? issue.EvidenceSummary : link.SafeSummary,
                    TrustState = issue.ExcludedFromTrustedRecommendations
                        ? TrustState.Conflicted
                        : issue.ResultingTrustState
                };
            case EvidenceLinkType.BomComparisonRun:
                var run = await dbContext.BomComparisonRuns
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == link.SourceId && item.TenantId == tenantId, cancellationToken);
                if (run is null)
                {
                    throw new RequestValidationException($"BOM comparison run '{link.SourceId}' was not found.");
                }

                var summary = link.SafeSummary;
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary =
                        $"Missing in EBOM {run.MissingInEbomCount}, quantity mismatches {run.QuantityMismatchCount}, unresolved identity {run.UnresolvedIdentityCount}.";
                }

                return link with
                {
                    SafeSummary = summary,
                    TrustState = run.UnresolvedIdentityCount > 0 ? TrustState.Provisional : TrustState.Trusted
                };
            case EvidenceLinkType.AiTrace:
                var trace = await dbContext.AiTraceRecords
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == link.SourceId && item.TenantId == tenantId, cancellationToken);
                if (trace is null)
                {
                    throw new RequestValidationException($"AI trace '{link.SourceId}' was not found.");
                }

                return link with
                {
                    SafeSummary = string.IsNullOrWhiteSpace(link.SafeSummary) ? trace.SafeSummary : link.SafeSummary
                };
            case EvidenceLinkType.GraphNode:
                return link with
                {
                    SafeSummary = string.IsNullOrWhiteSpace(link.SafeSummary)
                        ? $"Graph node {link.SourceId:N}."
                        : link.SafeSummary
                };
            default:
                return link;
        }
    }
}
