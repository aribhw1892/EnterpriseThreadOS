using System.Text.Json.Serialization;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.DataQuality;

public sealed class DataQualityIssue : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Title { get; set; }
    public required string IssueCode { get; set; }
    public required string NormalizedIssueCode { get; set; }
    public DataQualitySeverity Severity { get; set; }
    public DataQualityIssueStatus Status { get; set; } = DataQualityIssueStatus.Open;
    public DataQualityIssueOrigin Origin { get; set; }
    public DataQualityAffectedEntityType AffectedEntityType { get; set; }
    public Guid? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid? ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public Guid? ImportStagingGraphRunId { get; set; }
    public ImportStagingGraphRun? ImportStagingGraphRun { get; set; }
    public Guid? ImportValidationIssueId { get; set; }
    public ImportValidationIssue? ImportValidationIssue { get; set; }
    public Guid? ImportFileEvidenceId { get; set; }
    public ImportFileEvidence? ImportFileEvidence { get; set; }
    public Guid? IdentityCandidateLinkId { get; set; }
    public IdentityCandidateLink? IdentityCandidateLink { get; set; }
    public Guid? SecurityEventId { get; set; }
    public SecurityEvent? SecurityEvent { get; set; }
    public Guid? GraphNodeId { get; set; }
    public Guid? GraphRelationshipId { get; set; }
    public decimal TrustImpactPenalty { get; set; }
    public TrustState ResultingTrustState { get; set; } = TrustState.Unverified;
    public bool ExcludedFromTrustedRecommendations { get; set; }
    public DataQualityReviewPriority ReviewPriority { get; set; } = DataQualityReviewPriority.Normal;
    public bool ReviewTaskReady { get; set; }
    public string? ReviewTaskHint { get; set; }
    public DateTimeOffset? ReviewHookCreatedAt { get; set; }
    public string? UniqueSourceKey { get; set; }
    public required string EvidenceSummary { get; set; }
    public string? Rationale { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DataQualityIssueSourceLink> SourceLinks { get; set; } = [];
    public List<DataQualityTrustImpact> TrustImpacts { get; set; } = [];
}

public sealed class DataQualityIssueSourceLink : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DataQualityIssueId { get; set; }
    public DataQualityIssue? DataQualityIssue { get; set; }
    public DataQualitySourceLinkType SourceType { get; set; }
    public required string SourceId { get; set; }
    public string? Label { get; set; }
    public required string SafeSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DataQualityTrustImpact : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DataQualityIssueId { get; set; }
    public DataQualityIssue? DataQualityIssue { get; set; }
    public DataQualityAffectedEntityType TargetEntityType { get; set; }
    public Guid? GraphNodeId { get; set; }
    public Guid? GraphRelationshipId { get; set; }
    public Guid? IdentityCandidateLinkId { get; set; }
    public decimal ScorePenalty { get; set; }
    public TrustState ResultingTrustState { get; set; }
    public bool ExcludedFromTrustedRecommendations { get; set; }
    public required string BreakdownJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MonitoringIssueTypeDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string IssueTypeKey { get; set; }
    public required string NormalizedIssueTypeKey { get; set; }
    public required string DisplayName { get; set; }
    public required string SafeSummary { get; set; }
    public bool IsEnabled { get; set; }
    public bool AllowsLiveSourceScanning { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataQualitySeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataQualityIssueStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
    Dismissed = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataQualityIssueOrigin
{
    ImportValidation = 0,
    Manual = 1,
    SecurityEvent = 2,
    MonitoringPlaceholder = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataQualityAffectedEntityType
{
    ImportBatch = 0,
    ImportValidationIssue = 1,
    GraphNode = 2,
    GraphRelationship = 3,
    IdentityCandidate = 4,
    SecurityEvent = 5,
    GenericSource = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataQualitySourceLinkType
{
    ImportBatch = 0,
    ImportValidationIssue = 1,
    ImportFileEvidence = 2,
    ImportMappingVersion = 3,
    ImportStagingGraphRun = 4,
    IdentityCandidate = 5,
    SecurityEvent = 6,
    GraphNode = 7,
    GraphRelationship = 8,
    GenericSource = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataQualityReviewPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
