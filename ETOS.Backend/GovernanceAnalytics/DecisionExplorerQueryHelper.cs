using ETOS.Backend.Decisions;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Outcomes;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernanceAnalytics;

public sealed record DecisionExplorerFilter(
    string? Status = null,
    string? Participant = null,
    string? Search = null,
    string? Conflict = null,
    string? OutcomeKey = null,
    bool? HasOutcome = null,
    int? MinEvidenceCount = null);

public static class DecisionExplorerQueryHelper
{
    public static bool MatchesFilter(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        string artifactName,
        DecisionExplorerFilter filter,
        bool hasOutcomeCheckRun)
    {
        if (!string.IsNullOrWhiteSpace(filter.Status)
            && !string.Equals(payload.Status.ToString(), filter.Status.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Conflict)
            && !string.Equals(payload.ConflictState.ToString(), filter.Conflict.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.OutcomeKey)
            && !string.Equals(payload.OutcomeKey, filter.OutcomeKey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.HasOutcome == true
            && string.IsNullOrWhiteSpace(payload.OutcomeKey)
            && !hasOutcomeCheckRun)
        {
            return false;
        }

        if (filter.HasOutcome == false
            && (!string.IsNullOrWhiteSpace(payload.OutcomeKey) || hasOutcomeCheckRun))
        {
            return false;
        }

        if (filter.MinEvidenceCount.HasValue)
        {
            var evidenceCount = payload.EvidenceReferences?.Count ?? 0;
            if (evidenceCount < filter.MinEvidenceCount.Value)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Participant)
            && !payload.ParticipantUserIds?.Any(id => string.Equals(id.ToString(), filter.Participant.Trim(), StringComparison.OrdinalIgnoreCase)) == true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var title = payload.Title ?? artifactName;
            if (!title.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)
                && !artifactName.Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static async Task<IReadOnlySet<Guid>> LoadDecisionIdsWithOutcomeChecksAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        IReadOnlyCollection<Guid> decisionArtifactIds,
        CancellationToken cancellationToken)
    {
        if (decisionArtifactIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await dbContext.OutcomeCheckRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && decisionArtifactIds.Contains(run.DecisionArtifactId))
            .Select(run => run.DecisionArtifactId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
