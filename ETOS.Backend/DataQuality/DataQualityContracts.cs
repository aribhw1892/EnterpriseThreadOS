using ETOS.Backend.GraphMemory;

namespace ETOS.Backend.DataQuality;

public static class DataQualityPermissions
{
    public const string Read = "data_quality.read";
    public const string Manage = "data_quality.manage";
    public const string ReviewHook = "data_quality.review_hook";
    public const string Admin = "data_quality.admin";
}

public sealed record CreateDataQualityIssueRequest(
    string Title,
    string IssueCode,
    DataQualitySeverity Severity,
    DataQualityAffectedEntityType AffectedEntityType,
    Guid? ImportBatchId,
    Guid? ImportValidationIssueId,
    Guid? ImportFileEvidenceId,
    Guid? IdentityCandidateLinkId,
    Guid? GraphNodeId,
    Guid? GraphRelationshipId,
    string? GenericSourceId,
    string EvidenceSummary,
    string? Rationale);

public sealed record GenerateDataQualityIssuesFromImportResponse(
    Guid ImportBatchId,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyCollection<DataQualityIssueResponse> Issues);

public sealed record DataQualityIssueResponse(
    Guid Id,
    Guid TenantId,
    string Title,
    string IssueCode,
    DataQualitySeverity Severity,
    DataQualityIssueStatus Status,
    DataQualityIssueOrigin Origin,
    DataQualityAffectedEntityType AffectedEntityType,
    Guid? ImportBatchId,
    Guid? ImportMappingVersionId,
    Guid? ImportStagingGraphRunId,
    Guid? ImportValidationIssueId,
    Guid? ImportFileEvidenceId,
    Guid? IdentityCandidateLinkId,
    Guid? SecurityEventId,
    Guid? GraphNodeId,
    Guid? GraphRelationshipId,
    decimal TrustImpactPenalty,
    TrustState ResultingTrustState,
    bool ExcludedFromTrustedRecommendations,
    DataQualityReviewPriority ReviewPriority,
    bool ReviewTaskReady,
    string? ReviewTaskHint,
    DateTimeOffset? ReviewHookCreatedAt,
    string? UniqueSourceKey,
    string EvidenceSummary,
    string? Rationale,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<DataQualityIssueSourceLinkResponse> SourceLinks,
    IReadOnlyCollection<DataQualityTrustImpactResponse> TrustImpacts);

public sealed record DataQualityIssueSourceLinkResponse(
    Guid Id,
    Guid TenantId,
    Guid DataQualityIssueId,
    DataQualitySourceLinkType SourceType,
    string SourceId,
    string? Label,
    string SafeSummary,
    DateTimeOffset CreatedAt);

public sealed record DataQualityTrustImpactResponse(
    Guid Id,
    Guid TenantId,
    Guid DataQualityIssueId,
    DataQualityAffectedEntityType TargetEntityType,
    Guid? GraphNodeId,
    Guid? GraphRelationshipId,
    Guid? IdentityCandidateLinkId,
    decimal ScorePenalty,
    TrustState ResultingTrustState,
    bool ExcludedFromTrustedRecommendations,
    IReadOnlyDictionary<string, decimal> Breakdown,
    DateTimeOffset CreatedAt);

public sealed record MonitoringIssueTypeDefinitionResponse(
    Guid Id,
    Guid TenantId,
    string IssueTypeKey,
    string DisplayName,
    string SafeSummary,
    bool IsEnabled,
    bool AllowsLiveSourceScanning,
    DateTimeOffset CreatedAt);
