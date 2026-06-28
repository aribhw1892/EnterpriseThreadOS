using System.Text.Json;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Recommendations;

namespace ETOS.Backend.ReviewTasks;

public static class ReviewTaskPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ReviewTaskPayloadResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<ReviewTaskChainLinkResponse> chainLinks,
        IReadOnlyCollection<ReviewTaskCommentResponse> comments)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new ReviewTaskPayloadResponse(
            artifactId,
            versionId,
            versionLabel,
            document.Title!.Trim(),
            new ReviewTaskSourceReferenceResponse(document.SourceType, document.SourceReference!.Trim()),
            document.ReviewTaskType!.Trim(),
            document.Status,
            document.PrimaryOwnerUserId,
            document.AssignedRoleKey,
            document.Participants?.Select(item => new ReviewTaskParticipantResponse(item.UserId, item.Role)).ToList() ?? [],
            document.Priority,
            document.Severity,
            document.TrustState,
            document.ConflictState,
            document.ConfidenceScore,
            document.EvidenceReferences?.Select(item => new ReviewTaskEvidenceReferenceResponse(
                item.LinkId,
                item.EvidenceType,
                item.SourceId,
                item.SafeSummary,
                item.TrustState)).ToList() ?? [],
            document.ReviewTemplateVersionId,
            document.RecommendationArtifactId,
            document.RecommendationVersionId,
            document.SuggestedActionId,
            document.DataQualityIssueId,
            document.SecurityEventId,
            document.AccessRequestId,
            document.AiTraceId,
            document.ContextPackageId,
            document.DueDate,
            document.EscalationPlaceholder is null
                ? null
                : new ReviewTaskEscalationPlaceholderResponse(
                    document.EscalationPlaceholder.Enabled,
                    document.EscalationPlaceholder.EscalationTargetRoleKey,
                    document.EscalationPlaceholder.EscalationPolicyId,
                    document.EscalationPlaceholder.SlaPolicyVersion),
            document.PrerequisiteTaskIds ?? [],
            document.BlockingReason,
            chainLinks,
            comments,
            artifactReadinessState);
    }

    public static string Serialize(ReviewTaskPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static ReviewTaskPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<ReviewTaskPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Review task payload is invalid.");
        return document;
    }

    public static ReviewTaskPayloadDocument CreateDefault(
        string title,
        ReviewTaskSourceType sourceType,
        string sourceReference,
        string reviewTaskType,
        Guid? primaryOwnerUserId,
        string? assignedRoleKey,
        IReadOnlyCollection<ReviewTaskParticipantDocument>? participants,
        ReviewTaskPriority priority,
        RecommendationRiskState severity,
        TrustState trustState,
        RecommendationConflictState conflictState,
        decimal? confidenceScore,
        IReadOnlyCollection<ReviewTaskEvidenceReferenceDocument>? evidenceReferences,
        Guid? reviewTemplateVersionId,
        Guid? recommendationArtifactId,
        Guid? recommendationVersionId,
        Guid? suggestedActionId,
        Guid? dataQualityIssueId,
        Guid? securityEventId,
        Guid? accessRequestId,
        Guid? aiTraceId,
        Guid? contextPackageId,
        DateTimeOffset? dueDate,
        ReviewTaskEscalationPlaceholderDocument? escalationPlaceholder,
        ReviewTaskStatus status = ReviewTaskStatus.Open)
        => Normalize(new ReviewTaskPayloadDocument
        {
            Title = title.Trim(),
            SourceType = sourceType,
            SourceReference = sourceReference.Trim(),
            ReviewTaskType = reviewTaskType.Trim(),
            Status = status,
            PrimaryOwnerUserId = primaryOwnerUserId,
            AssignedRoleKey = assignedRoleKey?.Trim(),
            Participants = participants?.ToList() ?? [],
            Priority = priority,
            Severity = severity,
            TrustState = trustState,
            ConflictState = conflictState,
            ConfidenceScore = confidenceScore,
            EvidenceReferences = evidenceReferences?.ToList() ?? [],
            ReviewTemplateVersionId = reviewTemplateVersionId,
            RecommendationArtifactId = recommendationArtifactId,
            RecommendationVersionId = recommendationVersionId,
            SuggestedActionId = suggestedActionId,
            DataQualityIssueId = dataQualityIssueId,
            SecurityEventId = securityEventId,
            AccessRequestId = accessRequestId,
            AiTraceId = aiTraceId,
            ContextPackageId = contextPackageId,
            DueDate = dueDate,
            EscalationPlaceholder = escalationPlaceholder,
            PrerequisiteTaskIds = [],
            BlockingReason = null
        });

    public static void ValidateCore(ReviewTaskPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Title))
        {
            throw new RequestValidationException("title is required.");
        }

        if (string.IsNullOrWhiteSpace(document.SourceReference))
        {
            throw new RequestValidationException("sourceReference is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ReviewTaskType))
        {
            throw new RequestValidationException("reviewTaskType is required.");
        }
    }

    private static ReviewTaskPayloadDocument Normalize(ReviewTaskPayloadDocument document)
    {
        document.Title = document.Title?.Trim() ?? string.Empty;
        document.SourceReference = document.SourceReference?.Trim() ?? string.Empty;
        document.ReviewTaskType = document.ReviewTaskType?.Trim() ?? string.Empty;
        document.AssignedRoleKey = string.IsNullOrWhiteSpace(document.AssignedRoleKey) ? null : document.AssignedRoleKey.Trim();
        document.Participants ??= [];
        document.EvidenceReferences ??= [];
        document.PrerequisiteTaskIds ??= [];
        document.BlockingReason = string.IsNullOrWhiteSpace(document.BlockingReason) ? null : document.BlockingReason.Trim();
        return document;
    }

    public sealed class ReviewTaskPayloadDocument
    {
        public string? Title { get; set; }
        public ReviewTaskSourceType SourceType { get; set; }
        public string? SourceReference { get; set; }
        public string? ReviewTaskType { get; set; }
        public ReviewTaskStatus Status { get; set; } = ReviewTaskStatus.Open;
        public Guid? PrimaryOwnerUserId { get; set; }
        public string? AssignedRoleKey { get; set; }
        public List<ReviewTaskParticipantDocument>? Participants { get; set; }
        public ReviewTaskPriority Priority { get; set; } = ReviewTaskPriority.Normal;
        public RecommendationRiskState Severity { get; set; } = RecommendationRiskState.Medium;
        public TrustState TrustState { get; set; } = TrustState.Provisional;
        public RecommendationConflictState ConflictState { get; set; } = RecommendationConflictState.None;
        public decimal? ConfidenceScore { get; set; }
        public List<ReviewTaskEvidenceReferenceDocument>? EvidenceReferences { get; set; }
        public Guid? ReviewTemplateVersionId { get; set; }
        public Guid? RecommendationArtifactId { get; set; }
        public Guid? RecommendationVersionId { get; set; }
        public Guid? SuggestedActionId { get; set; }
        public Guid? DataQualityIssueId { get; set; }
        public Guid? SecurityEventId { get; set; }
        public Guid? AccessRequestId { get; set; }
        public Guid? AiTraceId { get; set; }
        public Guid? ContextPackageId { get; set; }
        public DateTimeOffset? DueDate { get; set; }
        public ReviewTaskEscalationPlaceholderDocument? EscalationPlaceholder { get; set; }
        public List<Guid>? PrerequisiteTaskIds { get; set; }
        public string? BlockingReason { get; set; }
    }

    public sealed class ReviewTaskParticipantDocument
    {
        public Guid UserId { get; set; }
        public ReviewTaskParticipantRole Role { get; set; }
    }

    public sealed class ReviewTaskEvidenceReferenceDocument
    {
        public Guid LinkId { get; set; }
        public EvidenceLinkType EvidenceType { get; set; }
        public Guid SourceId { get; set; }
        public string SafeSummary { get; set; } = string.Empty;
        public TrustState TrustState { get; set; } = TrustState.Provisional;
    }

    public sealed class ReviewTaskEscalationPlaceholderDocument
    {
        public bool Enabled { get; set; }
        public string? EscalationTargetRoleKey { get; set; }
        public string? EscalationPolicyId { get; set; }
        public string? SlaPolicyVersion { get; set; }
    }
}
