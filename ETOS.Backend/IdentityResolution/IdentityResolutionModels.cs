using System.Text.Json.Serialization;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Imports;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.IdentityResolution;

public sealed class IdentityResolutionRule : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public required string ObjectType { get; set; }
    public required string NormalizedObjectType { get; set; }
    public required string IdentityAttributeKeysJson { get; set; }
    public decimal AutoApproveThreshold { get; set; } = 0.97m;
    public decimal ReviewThreshold { get; set; } = 0.6m;
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<IdentityCandidateLink> Candidates { get; set; } = [];
}

public sealed class IdentityCandidateLink : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public Guid? ImportStagingGraphRunId { get; set; }
    public ImportStagingGraphRun? ImportStagingGraphRun { get; set; }
    public Guid? IdentityResolutionRuleId { get; set; }
    public IdentityResolutionRule? IdentityResolutionRule { get; set; }
    public Guid SourceGraphNodeId { get; set; }
    public Guid TargetGraphNodeId { get; set; }
    public required string SourceSystem { get; set; }
    public required string TargetSystem { get; set; }
    public required string SourceRecordId { get; set; }
    public required string TargetRecordId { get; set; }
    public required string ObjectType { get; set; }
    public required string NormalizedObjectType { get; set; }
    public required string IdentityKey { get; set; }
    public decimal ConfidenceScore { get; set; }
    public IdentityCandidateState State { get; set; } = IdentityCandidateState.Unverified;
    public TrustState TrustState { get; set; } = TrustState.Unverified;
    public bool ExcludedFromTrustedRecommendations { get; set; } = true;
    public Guid? GraphRelationshipId { get; set; }
    public required string EvidenceSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public List<IdentityResolutionDecision> Decisions { get; set; } = [];
}

public sealed class IdentityResolutionDecision : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IdentityCandidateLinkId { get; set; }
    public IdentityCandidateLink? IdentityCandidateLink { get; set; }
    public IdentityDecisionType DecisionType { get; set; }
    public TrustState ResultingTrustState { get; set; }
    public string? Rationale { get; set; }
    public Guid DecidedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class IdentityLearningEvidence : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? IdentityCandidateLinkId { get; set; }
    public IdentityCandidateLink? IdentityCandidateLink { get; set; }
    public Guid? IdentityResolutionDecisionId { get; set; }
    public IdentityResolutionDecision? IdentityResolutionDecision { get; set; }
    public IdentityDecisionType Outcome { get; set; }
    public required string IdentityKey { get; set; }
    public required string EvidenceSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrustScoreRecord : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid? IdentityCandidateLinkId { get; set; }
    public IdentityCandidateLink? IdentityCandidateLink { get; set; }
    public Guid? GraphNodeId { get; set; }
    public Guid? GraphRelationshipId { get; set; }
    public TrustScoreEntityType EntityType { get; set; }
    public decimal Score { get; set; }
    public TrustState TrustState { get; set; }
    public required string BreakdownJson { get; set; }
    public DateTimeOffset RecalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IdentityCandidateState
{
    Unverified = 0,
    Provisional = 1,
    Approved = 2,
    Rejected = 3,
    Conflicted = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IdentityDecisionType
{
    Approved = 0,
    Rejected = 1,
    Conflicted = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrustScoreEntityType
{
    GraphNode = 0,
    IdentityCandidate = 1,
    IdentityRelationship = 2
}
