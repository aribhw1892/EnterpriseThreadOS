using ETOS.Backend.GraphMemory;
using ETOS.Backend.Recommendations;

namespace ETOS.Backend.ReviewTasks;

public interface IReviewTaskPriorityDeriver
{
    ReviewTaskPriority Derive(
        RecommendationRiskState severity,
        TrustState trustState,
        RecommendationConflictState conflictState,
        ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument? template);
}

public sealed class ReviewTaskPriorityDeriver : IReviewTaskPriorityDeriver
{
    public ReviewTaskPriority Derive(
        RecommendationRiskState severity,
        TrustState trustState,
        RecommendationConflictState conflictState,
        ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument? template)
    {
        var severityScore = MapSeverity(severity);
        var trustScore = MapTrust(trustState);
        var conflictScore = MapConflict(conflictState);

        if (template?.PriorityRules is { Count: > 0 } rules)
        {
            var rule = rules[0];
            severityScore = Math.Max(severityScore, ParseWeight(rule.SeverityWeight, severity.ToString()));
            trustScore = Math.Max(trustScore, ParseWeight(rule.TrustWeight, trustState.ToString()));
            conflictScore = Math.Max(conflictScore, ParseWeight(rule.ConflictWeight, conflictState.ToString()));
        }

        var total = severityScore + trustScore + conflictScore;
        return total switch
        {
            >= 10 => ReviewTaskPriority.Critical,
            >= 7 => ReviewTaskPriority.High,
            >= 4 => ReviewTaskPriority.Normal,
            _ => ReviewTaskPriority.Low
        };
    }

    private static int MapSeverity(RecommendationRiskState severity)
        => severity switch
        {
            RecommendationRiskState.Critical => 4,
            RecommendationRiskState.High => 3,
            RecommendationRiskState.Medium => 2,
            _ => 1
        };

    private static int MapTrust(TrustState trustState)
        => trustState switch
        {
            TrustState.Conflicted => 3,
            TrustState.Provisional or TrustState.Unverified => 2,
            _ => 1
        };

    private static int MapConflict(RecommendationConflictState conflictState)
        => conflictState switch
        {
            RecommendationConflictState.Blocked => 3,
            RecommendationConflictState.Partial => 2,
            _ => 1
        };

    private static int ParseWeight(string weightExpression, string key)
    {
        foreach (var segment in weightExpression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[1], out var value))
            {
                return value;
            }
        }

        return 0;
    }
}
