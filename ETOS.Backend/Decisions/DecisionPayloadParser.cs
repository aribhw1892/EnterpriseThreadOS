using System.Text.Json;
using ETOS.Backend.Identity;
using ETOS.Backend.ReviewTasks;

namespace ETOS.Backend.Decisions;

public static class DecisionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(DecisionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static DecisionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<DecisionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Decision payload is invalid.");
        return document;
    }

    public static DecisionPayloadDocument CreateFromReviewTask(
        ReviewTaskPayloadParser.ReviewTaskPayloadDocument task,
        string outcomeKey,
        string? outcomeSummary,
        string? decisionReason,
        DecisionApprovalRuleSnapshotDocument approvalRuleSnapshot,
        DecisionStatus initialStatus,
        DecisionConflictState conflictState)
        => Normalize(new DecisionPayloadDocument
        {
            Title = task.Title?.Trim() ?? "Review decision",
            Status = initialStatus,
            OutcomeKey = outcomeKey.Trim(),
            OutcomeSummary = outcomeSummary?.Trim() ?? outcomeKey,
            DecisionReason = decisionReason?.Trim(),
            ReviewTaskArtifactId = null,
            ReviewTaskVersionId = null,
            ReviewTemplateVersionId = task.ReviewTemplateVersionId,
            RecommendationArtifactId = task.RecommendationArtifactId,
            RecommendationVersionId = task.RecommendationVersionId,
            SuggestedActionId = task.SuggestedActionId,
            DataQualityIssueId = task.DataQualityIssueId,
            SecurityEventId = task.SecurityEventId,
            AccessRequestId = task.AccessRequestId,
            AiTraceId = task.AiTraceId,
            ContextPackageId = task.ContextPackageId,
            ApprovalRuleSnapshot = approvalRuleSnapshot,
            ParticipantUserIds = BuildParticipantIds(task),
            EvidenceReferences = task.EvidenceReferences?.Select(item => new DecisionEvidenceReferenceDocument
            {
                LinkId = item.LinkId,
                EvidenceType = item.EvidenceType.ToString(),
                SourceId = item.SourceId,
                SafeSummary = item.SafeSummary,
                TrustState = item.TrustState.ToString()
            }).ToList() ?? [],
            ConflictState = conflictState,
            OutcomeTrackingRequired = approvalRuleSnapshot.OutcomeTrackingRequired,
            OutcomeTaxonomyVersionId = approvalRuleSnapshot.OutcomeTaxonomyVersionId,
            SourceType = task.SourceType.ToString(),
            ReviewTaskType = task.ReviewTaskType?.Trim() ?? string.Empty
        });

    public static void ApplyReviewTaskIds(DecisionPayloadDocument document, Guid taskArtifactId, Guid taskVersionId)
    {
        document.ReviewTaskArtifactId = taskArtifactId;
        document.ReviewTaskVersionId = taskVersionId;
    }

    public static DecisionApprovalRuleSnapshotDocument FromTemplateApprovalRule(
        ReviewTaskTemplatePayloadParser.ReviewTaskTemplateApprovalRuleDocument? rule)
    {
        if (rule is null)
        {
            return DefaultApprovalRule();
        }

        return new DecisionApprovalRuleSnapshotDocument
        {
            Mode = rule.Mode,
            RequiredRoles = rule.RequiredRoles?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            OutcomeTaxonomyVersionId = rule.OutcomeTaxonomyVersionId,
            OutcomeTrackingRequired = rule.OutcomeTrackingRequired
        };
    }

    public static DecisionApprovalRuleSnapshotDocument DefaultApprovalRule()
        => new()
        {
            Mode = DecisionApprovalRuleMode.SingleApprover,
            RequiredRoles = [],
            OutcomeTrackingRequired = false
        };

    private static List<Guid> BuildParticipantIds(ReviewTaskPayloadParser.ReviewTaskPayloadDocument task)
    {
        var ids = new HashSet<Guid>();
        if (task.PrimaryOwnerUserId.HasValue)
        {
            ids.Add(task.PrimaryOwnerUserId.Value);
        }

        foreach (var participant in task.Participants ?? [])
        {
            ids.Add(participant.UserId);
        }

        return ids.ToList();
    }

    private static DecisionPayloadDocument Normalize(DecisionPayloadDocument document)
    {
        document.Title = document.Title?.Trim() ?? string.Empty;
        document.OutcomeKey = document.OutcomeKey?.Trim() ?? string.Empty;
        document.OutcomeSummary = document.OutcomeSummary?.Trim() ?? string.Empty;
        document.DecisionReason = string.IsNullOrWhiteSpace(document.DecisionReason) ? null : document.DecisionReason.Trim();
        document.ApprovalRuleSnapshot ??= DefaultApprovalRule();
        document.ParticipantUserIds ??= [];
        document.EvidenceReferences ??= [];
        document.SourceType ??= ReviewTaskSourceType.Manual.ToString();
        document.ReviewTaskType ??= string.Empty;
        return document;
    }

    public sealed class DecisionPayloadDocument
    {
        public string? Title { get; set; }
        public DecisionStatus Status { get; set; } = DecisionStatus.PendingVotes;
        public string? OutcomeKey { get; set; }
        public string? OutcomeSummary { get; set; }
        public string? DecisionReason { get; set; }
        public Guid? ReviewTaskArtifactId { get; set; }
        public Guid? ReviewTaskVersionId { get; set; }
        public Guid? ReviewTemplateVersionId { get; set; }
        public Guid? RecommendationArtifactId { get; set; }
        public Guid? RecommendationVersionId { get; set; }
        public Guid? SuggestedActionId { get; set; }
        public Guid? DataQualityIssueId { get; set; }
        public Guid? SecurityEventId { get; set; }
        public Guid? AccessRequestId { get; set; }
        public Guid? AiTraceId { get; set; }
        public Guid? ContextPackageId { get; set; }
        public Guid? ParentDecisionArtifactId { get; set; }
        public DecisionApprovalRuleSnapshotDocument? ApprovalRuleSnapshot { get; set; }
        public List<Guid>? ParticipantUserIds { get; set; }
        public List<DecisionEvidenceReferenceDocument>? EvidenceReferences { get; set; }
        public DecisionConflictState ConflictState { get; set; } = DecisionConflictState.None;
        public bool OutcomeTrackingRequired { get; set; }
        public Guid? OutcomeTaxonomyVersionId { get; set; }
        public DateTimeOffset? FinalizedAt { get; set; }
        public Guid? FinalizedByUserId { get; set; }
        public string? SourceType { get; set; }
        public string? ReviewTaskType { get; set; }
    }

    public sealed class DecisionApprovalRuleSnapshotDocument
    {
        public DecisionApprovalRuleMode Mode { get; set; } = DecisionApprovalRuleMode.SingleApprover;
        public List<string>? RequiredRoles { get; set; }
        public Guid? OutcomeTaxonomyVersionId { get; set; }
        public bool OutcomeTrackingRequired { get; set; }
    }

    public sealed class DecisionEvidenceReferenceDocument
    {
        public Guid LinkId { get; set; }
        public string EvidenceType { get; set; } = string.Empty;
        public Guid SourceId { get; set; }
        public string SafeSummary { get; set; } = string.Empty;
        public string TrustState { get; set; } = string.Empty;
    }
}
