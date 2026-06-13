using ETOS.Backend.GraphMemory;

namespace ETOS.Backend.Recommendations;

public static class RecommendationReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateForReviewed(RecommendationPayloadResponse payload)
    {
        var notes = new List<string>();

        if (payload.EvidenceLinks.Count == 0)
        {
            notes.Add("At least one evidence link is required before marking reviewed.");
        }

        foreach (var link in payload.EvidenceLinks)
        {
            if (string.IsNullOrWhiteSpace(link.SafeSummary))
            {
                notes.Add($"Evidence link '{link.LinkId}' requires safeSummary.");
            }

            if (link.SourceId == Guid.Empty)
            {
                notes.Add($"Evidence link '{link.LinkId}' requires sourceId.");
            }
        }

        notes.AddRange(ValidateSuggestedActions(payload.SuggestedActions));
        return notes;
    }

    public static IReadOnlyCollection<string> ValidateForReady(
        RecommendationPayloadResponse payload,
        bool excludedFromTrustedRecommendations)
    {
        var notes = new List<string>();

        if (payload.LifecycleStatus != RecommendationLifecycleStatus.Reviewed)
        {
            notes.Add("Recommendation must be reviewed before it can be marked ready.");
        }

        notes.AddRange(ValidateForReviewed(payload));

        if (payload.EvidenceLinks.Any(link => link.TrustState == TrustState.Conflicted))
        {
            notes.Add("Conflicted evidence blocks trusted recommendation readiness.");
        }

        if (payload.EvidenceLinks.Any(link => link.TrustState == TrustState.Unverified))
        {
            notes.Add("Unverified evidence blocks trusted recommendation readiness.");
        }

        if (excludedFromTrustedRecommendations)
        {
            notes.Add("Linked evidence is excluded from trusted recommendations.");
        }

        if (payload.ConflictState == RecommendationConflictState.Blocked)
        {
            notes.Add("Recommendation conflict state is blocked.");
        }

        return notes;
    }

    private static IReadOnlyCollection<string> ValidateSuggestedActions(
        IReadOnlyCollection<RecommendationSuggestedActionResponse> suggestedActions)
    {
        var notes = new List<string>();

        foreach (var action in suggestedActions)
        {
            if (string.IsNullOrWhiteSpace(action.Title))
            {
                notes.Add($"Suggested action '{action.ActionId}' requires title.");
            }

            if (string.IsNullOrWhiteSpace(action.Kind))
            {
                notes.Add($"Suggested action '{action.ActionId}' requires kind.");
            }
        }

        return notes;
    }
}
