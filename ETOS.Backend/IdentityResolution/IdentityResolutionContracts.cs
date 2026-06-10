using ETOS.Backend.GraphMemory;

namespace ETOS.Backend.IdentityResolution;

public static class IdentityResolutionPermissions
{
    public const string Read = "identity_resolution.read";
    public const string Manage = "identity_resolution.manage";
    public const string Review = "identity_resolution.review";
    public const string Admin = "identity_resolution.admin";
}

public sealed record CreateIdentityResolutionRuleRequest(
    string Name,
    string ObjectType,
    IReadOnlyCollection<string> IdentityAttributeKeys,
    decimal AutoApproveThreshold,
    decimal ReviewThreshold);

public sealed record GenerateIdentityCandidatesRequest(Guid? RuleId);

public sealed record IdentityReviewDecisionRequest(string? Rationale);

public sealed record IdentityResolutionRuleResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string ObjectType,
    IReadOnlyCollection<string> IdentityAttributeKeys,
    decimal AutoApproveThreshold,
    decimal ReviewThreshold,
    bool IsActive,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record IdentityCandidateGenerationResponse(
    Guid ImportBatchId,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyCollection<IdentityCandidateLinkResponse> Candidates);

public sealed record IdentityCandidateLinkResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid ImportMappingVersionId,
    Guid? ImportStagingGraphRunId,
    Guid? IdentityResolutionRuleId,
    Guid SourceGraphNodeId,
    Guid TargetGraphNodeId,
    string SourceSystem,
    string TargetSystem,
    string SourceRecordId,
    string TargetRecordId,
    string ObjectType,
    string IdentityKey,
    decimal ConfidenceScore,
    IdentityCandidateState State,
    TrustState TrustState,
    bool ExcludedFromTrustedRecommendations,
    Guid? GraphRelationshipId,
    string EvidenceSummary,
    DateTimeOffset CreatedAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    IReadOnlyCollection<IdentityResolutionDecisionResponse> Decisions);

public sealed record IdentityResolutionDecisionResponse(
    Guid Id,
    Guid TenantId,
    Guid IdentityCandidateLinkId,
    IdentityDecisionType DecisionType,
    TrustState ResultingTrustState,
    string? Rationale,
    Guid DecidedByUserId,
    DateTimeOffset CreatedAt);

public sealed record TrustScoreRecordResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid? IdentityCandidateLinkId,
    Guid? GraphNodeId,
    Guid? GraphRelationshipId,
    TrustScoreEntityType EntityType,
    decimal Score,
    TrustState TrustState,
    IReadOnlyDictionary<string, decimal> Breakdown,
    DateTimeOffset RecalculatedAt);
