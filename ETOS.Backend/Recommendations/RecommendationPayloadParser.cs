using System.Text.Json;
using System.Text.Json.Serialization;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Recommendations;

public static class RecommendationPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static RecommendationPayloadResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactReadinessState,
        string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        ValidateCore(payload);

        return new RecommendationPayloadResponse(
            artifactId,
            versionId,
            versionLabel,
            payload.Title!.Trim(),
            payload.Summary!.Trim(),
            payload.RecommendationType,
            payload.CreationSource,
            payload.SourceReference is null
                ? null
                : new RecommendationSourceReferenceResponse(payload.SourceReference.Kind.Trim(), payload.SourceReference.Id),
            payload.RiskState,
            payload.CapabilityState,
            payload.TrustState,
            payload.ConflictState,
            payload.LifecycleStatus,
            payload.EvidenceLinks.Select(MapEvidence).ToList(),
            payload.SuggestedActions.Select(MapAction).ToList(),
            payload.RelatedObjects.Select(item => new RecommendationRelatedObjectResponse(item.GraphNodeId, item.ObjectType?.Trim())).ToList(),
            new RecommendationExplainabilityResponse(
                payload.Explainability?.AiTraceId,
                payload.Explainability?.ContextPackageId,
                payload.Explainability?.RetrievalRunId),
            payload.OutcomeTrackingRequired,
            payload.UniqueSourceKey?.Trim(),
            artifactReadinessState);
    }

    public static string Serialize(RecommendationPayloadDocument payload)
        => JsonSerializer.Serialize(Normalize(payload), JsonOptions);

    public static RecommendationPayloadDocument Deserialize(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<RecommendationPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Recommendation payload is invalid.");
        return payload;
    }

    public static RecommendationPayloadDocument CreateDefault(
        string title,
        string summary,
        RecommendationType recommendationType,
        RecommendationCreationSource creationSource,
        RecommendationRiskState riskState,
        RecommendationCapabilityState capabilityState,
        IReadOnlyCollection<RecommendationEvidenceLinkDocument> evidenceLinks,
        IReadOnlyCollection<RecommendationSuggestedActionDocument> suggestedActions,
        IReadOnlyCollection<RecommendationRelatedObjectDocument>? relatedObjects,
        RecommendationExplainabilityDocument? explainability,
        bool outcomeTrackingRequired,
        RecommendationSourceReferenceDocument? sourceReference,
        string? uniqueSourceKey)
    {
        var trustState = ComputeAggregateTrustState(evidenceLinks);
        var conflictState = ComputeConflictState(evidenceLinks);

        return Normalize(new RecommendationPayloadDocument
        {
            Title = title.Trim(),
            Summary = summary.Trim(),
            RecommendationType = recommendationType,
            CreationSource = creationSource,
            SourceReference = sourceReference,
            RiskState = riskState,
            CapabilityState = capabilityState,
            TrustState = trustState,
            ConflictState = conflictState,
            LifecycleStatus = RecommendationLifecycleStatus.Draft,
            EvidenceLinks = evidenceLinks.ToList(),
            SuggestedActions = suggestedActions.ToList(),
            RelatedObjects = relatedObjects?.ToList() ?? [],
            Explainability = explainability,
            OutcomeTrackingRequired = outcomeTrackingRequired,
            UniqueSourceKey = uniqueSourceKey?.Trim()
        });
    }

    public static TrustState ComputeAggregateTrustState(IReadOnlyCollection<RecommendationEvidenceLinkDocument> evidenceLinks)
    {
        if (evidenceLinks.Any(link => link.TrustState == TrustState.Conflicted))
        {
            return TrustState.Conflicted;
        }

        if (evidenceLinks.Any(link => link.TrustState == TrustState.Unverified))
        {
            return TrustState.Unverified;
        }

        if (evidenceLinks.Any(link => link.TrustState == TrustState.Provisional))
        {
            return TrustState.Provisional;
        }

        return evidenceLinks.Count > 0 ? TrustState.Trusted : TrustState.Unverified;
    }

    public static RecommendationConflictState ComputeConflictState(IReadOnlyCollection<RecommendationEvidenceLinkDocument> evidenceLinks)
    {
        if (evidenceLinks.All(link => link.TrustState == TrustState.Conflicted))
        {
            return RecommendationConflictState.Blocked;
        }

        if (evidenceLinks.Any(link => link.TrustState == TrustState.Conflicted || link.TrustState == TrustState.Unverified))
        {
            return RecommendationConflictState.Partial;
        }

        return RecommendationConflictState.None;
    }

    private static void ValidateCore(RecommendationPayloadDocument payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new RequestValidationException("Recommendation title is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.Summary))
        {
            throw new RequestValidationException("Recommendation summary is required.");
        }
    }

    private static RecommendationEvidenceLinkResponse MapEvidence(RecommendationEvidenceLinkDocument link)
        => new(
            link.LinkId == Guid.Empty ? Guid.NewGuid() : link.LinkId,
            link.EvidenceType,
            link.SourceId,
            link.SafeSummary.Trim(),
            link.TrustState,
            link.PermissionFiltered);

    private static RecommendationSuggestedActionResponse MapAction(RecommendationSuggestedActionDocument action)
        => new(
            action.ActionId == Guid.Empty ? Guid.NewGuid() : action.ActionId,
            action.Title.Trim(),
            action.Kind.Trim(),
            action.RiskScore,
            action.RequiredReviewPath?.Trim(),
            action.Status,
            action.Description?.Trim());

    private static RecommendationPayloadDocument Normalize(RecommendationPayloadDocument payload)
    {
        payload.EvidenceLinks = payload.EvidenceLinks
            .Select(link => link with
            {
                LinkId = link.LinkId == Guid.Empty ? Guid.NewGuid() : link.LinkId,
                SafeSummary = link.SafeSummary.Trim()
            })
            .ToList();

        payload.SuggestedActions = payload.SuggestedActions
            .Select(action => action with
            {
                ActionId = action.ActionId == Guid.Empty ? Guid.NewGuid() : action.ActionId,
                Title = action.Title.Trim(),
                Kind = action.Kind.Trim()
            })
            .ToList();

        payload.TrustState = ComputeAggregateTrustState(payload.EvidenceLinks);
        payload.ConflictState = ComputeConflictState(payload.EvidenceLinks);
        return payload;
    }

    public sealed class RecommendationPayloadDocument
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public RecommendationType RecommendationType { get; set; }
        public RecommendationCreationSource CreationSource { get; set; }
        public RecommendationSourceReferenceDocument? SourceReference { get; set; }
        public RecommendationRiskState RiskState { get; set; } = RecommendationRiskState.Medium;
        public RecommendationCapabilityState CapabilityState { get; set; } = RecommendationCapabilityState.ReadOnlyAnalysis;
        public TrustState TrustState { get; set; } = TrustState.Unverified;
        public RecommendationConflictState ConflictState { get; set; } = RecommendationConflictState.None;
        public RecommendationLifecycleStatus LifecycleStatus { get; set; } = RecommendationLifecycleStatus.Draft;
        public List<RecommendationEvidenceLinkDocument> EvidenceLinks { get; set; } = [];
        public List<RecommendationSuggestedActionDocument> SuggestedActions { get; set; } = [];
        public List<RecommendationRelatedObjectDocument> RelatedObjects { get; set; } = [];
        public RecommendationExplainabilityDocument? Explainability { get; set; }
        public bool OutcomeTrackingRequired { get; set; } = true;
        public string? UniqueSourceKey { get; set; }
        public bool CreatedFromChat { get; set; }
    }

    public sealed record RecommendationSourceReferenceDocument(string Kind, Guid Id);

    public sealed record RecommendationEvidenceLinkDocument(
        Guid LinkId,
        EvidenceLinkType EvidenceType,
        Guid SourceId,
        string SafeSummary,
        TrustState TrustState,
        bool PermissionFiltered);

    public sealed record RecommendationSuggestedActionDocument(
        Guid ActionId,
        string Title,
        string Kind,
        RecommendationRiskState RiskScore,
        string? RequiredReviewPath,
        SuggestedActionStatus Status,
        string? Description);

    public sealed record RecommendationRelatedObjectDocument(Guid? GraphNodeId, string? ObjectType);

    public sealed record RecommendationExplainabilityDocument(
        Guid? AiTraceId,
        Guid? ContextPackageId,
        Guid? RetrievalRunId);
}
