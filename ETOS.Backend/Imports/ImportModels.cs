using System.Text.Json.Serialization;
using ETOS.Backend.Ontology;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Imports;

public sealed class ImportBatch : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string SourceSystem { get; set; }
    public required string NormalizedSourceSystem { get; set; }
    public string? Description { get; set; }
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Created;
    public Guid ActiveModelPackageVersionId { get; set; }
    public string? ActiveModelPackageKey { get; set; }
    public string? ActiveModelPackageVersionLabel { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ValidatedAt { get; set; }
    public DateTimeOffset? StagedAt { get; set; }
    public List<ImportFileEvidence> FileEvidence { get; set; } = [];
    public List<ImportMappingVersion> MappingVersions { get; set; } = [];
    public List<ImportValidationIssue> ValidationIssues { get; set; } = [];
    public List<ImportStagingGraphRun> StagingRuns { get; set; } = [];
    public List<ImportPromotionRun> PromotionRuns { get; set; } = [];
    public List<RejectedStagingSummary> RejectedStagingSummaries { get; set; } = [];
    public List<BomComparisonRun> BomComparisonRuns { get; set; } = [];
}

public sealed class ImportFileEvidence : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public required string StorageKey { get; set; }
    public required string Sha256Checksum { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ImportMappingVersion : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid ModelPackageVersionId { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public string? Summary { get; set; }
    public ImportMappingState State { get; set; } = ImportMappingState.Draft;
    public required string SuggestionProvider { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public List<ImportColumnMapping> ColumnMappings { get; set; } = [];
    public List<ImportLifecycleMapping> LifecycleMappings { get; set; } = [];
}

public sealed class ImportColumnMapping : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public required string SourceColumn { get; set; }
    public required string NormalizedSourceColumn { get; set; }
    public required string CanonicalObjectType { get; set; }
    public required string NormalizedCanonicalObjectType { get; set; }
    public string? CanonicalAttributeKey { get; set; }
    public string? NormalizedCanonicalAttributeKey { get; set; }
    public bool IsIdentityField { get; set; }
    public bool IsRequired { get; set; }
}

public sealed class ImportLifecycleMapping : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public required string SourceValue { get; set; }
    public required string NormalizedSourceValue { get; set; }
    public required string CanonicalLifecycleKey { get; set; }
    public required string NormalizedCanonicalLifecycleKey { get; set; }
}

public sealed class ImportValidationIssue : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid? ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public ImportIssueSeverity Severity { get; set; } = ImportIssueSeverity.Error;
    public int? RowNumber { get; set; }
    public string? SourceColumn { get; set; }
    public string? CanonicalObjectType { get; set; }
    public required string IssueCode { get; set; }
    public required string Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ImportStagingGraphRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public ImportStagingRunStatus Status { get; set; } = ImportStagingRunStatus.Running;
    public int NodeCount { get; set; }
    public int RelationshipCount { get; set; }
    public string? GraphNodeIdsJson { get; set; }
    public string? GraphRelationshipIdsJson { get; set; }
    public string? FailureSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ImportPromotionRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid ImportStagingGraphRunId { get; set; }
    public ImportStagingGraphRun? ImportStagingGraphRun { get; set; }
    public ImportPromotionRunStatus Status { get; set; } = ImportPromotionRunStatus.Running;
    public int PromotedNodeCount { get; set; }
    public int PromotedRelationshipCount { get; set; }
    public string SourceEvidenceIdsJson { get; set; } = "[]";
    public Guid? AuditRecordId { get; set; }
    public string? FailureSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class RejectedStagingSummary : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public Guid ImportStagingGraphRunId { get; set; }
    public ImportStagingGraphRun? ImportStagingGraphRun { get; set; }
    public required string ValidationSummaryJson { get; set; }
    public required string DecisionSummaryJson { get; set; }
    public int NodeCount { get; set; }
    public int RelationshipCount { get; set; }
    public string SourceEvidenceIdsJson { get; set; } = "[]";
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BomComparisonRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public string? SourceContext { get; set; }
    public required string CadSummaryJson { get; set; }
    public required string EbomSummaryJson { get; set; }
    public int MissingInCadCount { get; set; }
    public int MissingInEbomCount { get; set; }
    public int QuantityMismatchCount { get; set; }
    public int UsageReferenceMismatchCount { get; set; }
    public int UnresolvedIdentityCount { get; set; }
    public required string ResultJson { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportBatchStatus
{
    Created = 0,
    FileUploaded = 1,
    MappingDrafted = 2,
    MappingApproved = 3,
    Validated = 4,
    Staged = 5,
    Failed = 6,
    Promoted = 7,
    Rejected = 8
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportMappingState
{
    Draft = 0,
    Approved = 1,
    Rejected = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportIssueSeverity
{
    Warning = 0,
    Error = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportStagingRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportPromotionRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2
}

internal sealed record ImportModelContext(
    ModelPackageVersion ModelPackage,
    OntologyVersion Ontology,
    LifecycleVocabularyVersion LifecycleVocabulary,
    AttributeSchemaVersion AttributeSchema);
