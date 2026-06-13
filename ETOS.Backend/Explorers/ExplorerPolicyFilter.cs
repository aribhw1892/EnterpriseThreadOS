using ETOS.Backend.Classification;
using ETOS.Backend.GraphMemory;

namespace ETOS.Backend.Explorers;

public sealed record ExplorerPolicyFilterResult(
    IReadOnlyDictionary<string, string> AllowedAttributes,
    string SafeSummary,
    bool IsDenied,
    string? DeniedReason);

public sealed class ExplorerPolicyFilter(IClassificationPolicyService classificationPolicyService)
{
    public async Task<ExplorerPolicyFilterResult> FilterNodeAsync(
        BaseNode node,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var classificationKey = ResolveClassificationKey(node.Attributes);
        var attributeKey = node.Attributes.Keys.FirstOrDefault(key =>
            !string.Equals(key, "classificationKey", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(key, "safeSummary", StringComparison.OrdinalIgnoreCase));
        var safeSummary = ResolveSafeSummary(node);
        var contextId = node.NodeId.ToString();

        var evaluation = await classificationPolicyService.EvaluateAsync(
            new EvaluatePolicyRequest(
                "explorers.graph_node_summary",
                policyKey,
                [
                    new PolicyEvaluationContextItem(
                        contextId,
                        node.ObjectType,
                        classificationKey,
                        attributeKey,
                        null,
                        safeSummary)
                ]),
            cancellationToken);

        if (evaluation.DeniedSummaries.Count > 0)
        {
            var denied = evaluation.DeniedSummaries.First();
            return new ExplorerPolicyFilterResult(
                new Dictionary<string, string>(),
                denied.SafeSummary,
                true,
                denied.Reason);
        }

        var allowedAttributes = node.Attributes
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Where(pair => !string.Equals(pair.Key, "safeSummary", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value!.Trim(),
                StringComparer.OrdinalIgnoreCase);

        return new ExplorerPolicyFilterResult(
            allowedAttributes,
            evaluation.AllowedContext.FirstOrDefault()?.SafeSummary ?? safeSummary,
            false,
            null);
    }

    public static bool MeetsTrustFilter(BaseNode node, TrustState minimumTrustState)
    {
        return node.TrustState >= minimumTrustState;
    }

    public static string ResolveClassificationKey(IReadOnlyDictionary<string, string?> attributes)
    {
        if (attributes.TryGetValue("classificationKey", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return "internal";
    }

    public static string ResolveSafeSummary(BaseNode node)
    {
        if (node.Attributes.TryGetValue("safeSummary", out var summary) && !string.IsNullOrWhiteSpace(summary))
        {
            return summary.Trim();
        }

        return $"{node.ObjectType} node in {node.GraphSpace} space with trust state {node.TrustState}.";
    }
}
