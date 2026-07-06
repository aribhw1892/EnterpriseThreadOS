using ETOS.Backend.ReviewTasks;

namespace ETOS.Backend.Decisions;

public sealed record DecisionConflictEvaluation(
    DecisionStatus Status,
    DecisionConflictState ConflictState,
    string OutcomeKey,
    string OutcomeSummary,
    bool IsFinalized);

public interface IDecisionConflictResolver
{
    DecisionConflictEvaluation Evaluate(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        IReadOnlyCollection<DecisionVote> votes,
        IReadOnlyDictionary<Guid, ReviewTaskParticipantRole> participantRoles);
}

public sealed class DecisionConflictResolver : IDecisionConflictResolver
{
    private static readonly HashSet<string> TerminalOutcomeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "no_action",
        "defer",
        "duplicate",
        "known_exception"
    };

    public DecisionConflictEvaluation Evaluate(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        IReadOnlyCollection<DecisionVote> votes,
        IReadOnlyDictionary<Guid, ReviewTaskParticipantRole> participantRoles)
    {
        var outcomeKey = payload.OutcomeKey ?? string.Empty;
        var outcomeSummary = payload.OutcomeSummary ?? outcomeKey;
        var mode = payload.ApprovalRuleSnapshot?.Mode ?? DecisionApprovalRuleMode.SingleApprover;

        if (mode == DecisionApprovalRuleMode.SingleApprover || TerminalOutcomeKeys.Contains(outcomeKey))
        {
            return Finalized(payload, outcomeKey, outcomeSummary, DecisionConflictState.None);
        }

        var requiredVoters = ResolveRequiredVoters(payload, participantRoles);
        if (requiredVoters.Count == 0)
        {
            return Finalized(payload, outcomeKey, outcomeSummary, DecisionConflictState.None);
        }

        var relevantVotes = votes.Where(vote => requiredVoters.Contains(vote.UserId)).ToList();
        var approveCount = relevantVotes.Count(vote => vote.Vote == DecisionVoteKind.Approve);
        var rejectCount = relevantVotes.Count(vote => vote.Vote is DecisionVoteKind.Reject or DecisionVoteKind.Dissent);
        var votedCount = relevantVotes.Count;

        return mode switch
        {
            DecisionApprovalRuleMode.AnyOne when approveCount > 0
                => Finalized(payload, "accept", "Approved by any required approver.", DecisionConflictState.None),
            DecisionApprovalRuleMode.AnyOne when votedCount == requiredVoters.Count && approveCount == 0
                => Finalized(payload, "reject", "Rejected by all required approvers.", DecisionConflictState.None),
            DecisionApprovalRuleMode.Majority when votedCount == requiredVoters.Count
                => EvaluateMajority(payload, approveCount, rejectCount, requiredVoters.Count),
            DecisionApprovalRuleMode.AllRequired or DecisionApprovalRuleMode.RoleBased
                => EvaluateAllRequired(payload, relevantVotes, requiredVoters, approveCount, rejectCount, votedCount),
            _ when approveCount > 0 && rejectCount > 0
                => Blocked(payload, outcomeKey, outcomeSummary),
            _ when votedCount < requiredVoters.Count
                => Pending(payload, outcomeKey, outcomeSummary),
            _ => Finalized(payload, outcomeKey, outcomeSummary, DecisionConflictState.None)
        };
    }

    private static DecisionConflictEvaluation EvaluateMajority(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        int approveCount,
        int rejectCount,
        int requiredCount)
    {
        if (approveCount > rejectCount)
        {
            return Finalized(payload, "accept", "Approved by majority.", DecisionConflictState.Resolved);
        }

        if (rejectCount > approveCount)
        {
            return Finalized(payload, "reject", "Rejected by majority.", DecisionConflictState.Resolved);
        }

        return Blocked(payload, payload.OutcomeKey ?? "reject", "Majority vote tied.");
    }

    private static DecisionConflictEvaluation EvaluateAllRequired(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        IReadOnlyCollection<DecisionVote> relevantVotes,
        IReadOnlyCollection<Guid> requiredVoters,
        int approveCount,
        int rejectCount,
        int votedCount)
    {
        if (approveCount > 0 && rejectCount > 0)
        {
            return Blocked(payload, payload.OutcomeKey ?? "reject", "Conflicting approver votes.");
        }

        if (votedCount < requiredVoters.Count)
        {
            return Pending(payload, payload.OutcomeKey ?? string.Empty, payload.OutcomeSummary ?? string.Empty);
        }

        if (approveCount == requiredVoters.Count)
        {
            return Finalized(payload, "accept", "Approved by all required approvers.", DecisionConflictState.None);
        }

        return Finalized(payload, "reject", "Rejected by required approvers.", DecisionConflictState.None);
    }

    private static HashSet<Guid> ResolveRequiredVoters(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        IReadOnlyDictionary<Guid, ReviewTaskParticipantRole> participantRoles)
    {
        var participantIds = payload.ParticipantUserIds ?? [];
        if (participantIds.Count == 0)
        {
            return [];
        }

        var mode = payload.ApprovalRuleSnapshot?.Mode ?? DecisionApprovalRuleMode.SingleApprover;
        if (mode is DecisionApprovalRuleMode.SingleApprover or DecisionApprovalRuleMode.AnyOne or DecisionApprovalRuleMode.Majority)
        {
            return participantIds.ToHashSet();
        }

        var requiredRoleNames = payload.ApprovalRuleSnapshot?.RequiredRoles?
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        if (requiredRoleNames.Count == 0)
        {
            return participantIds.ToHashSet();
        }

        return participantIds
            .Where(id => participantRoles.TryGetValue(id, out var role)
                && requiredRoleNames.Contains(ToRoleKey(role)))
            .ToHashSet();
    }

    private static string ToRoleKey(ReviewTaskParticipantRole role)
        => role switch
        {
            ReviewTaskParticipantRole.PrimaryOwner => "primaryowner",
            ReviewTaskParticipantRole.Reviewer => "reviewer",
            ReviewTaskParticipantRole.Approver => "approver",
            ReviewTaskParticipantRole.Observer => "observer",
            ReviewTaskParticipantRole.Contributor => "contributor",
            ReviewTaskParticipantRole.EscalationContact => "escalationcontact",
            _ => role.ToString().ToLowerInvariant()
        };

    private static DecisionConflictEvaluation Finalized(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        string outcomeKey,
        string outcomeSummary,
        DecisionConflictState conflictState)
        => new(DecisionStatus.Finalized, conflictState, outcomeKey, outcomeSummary, true);

    private static DecisionConflictEvaluation Pending(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        string outcomeKey,
        string outcomeSummary)
        => new(DecisionStatus.PendingVotes, DecisionConflictState.None, outcomeKey, outcomeSummary, false);

    private static DecisionConflictEvaluation Blocked(
        DecisionPayloadParser.DecisionPayloadDocument payload,
        string outcomeKey,
        string outcomeSummary)
        => new(DecisionStatus.BlockedConflict, DecisionConflictState.Blocked, outcomeKey, outcomeSummary, false);
}
