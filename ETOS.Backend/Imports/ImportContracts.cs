namespace ETOS.Backend.Imports;

public static class ImportPermissions
{
    public const string Read = "imports.read";
    public const string Manage = "imports.manage";
    public const string Approve = "imports.approve";
    public const string Stage = "imports.stage";
    public const string Admin = "imports.admin";
}

public sealed record CreateImportBatchRequest(
    string SourceSystem,
    string? Description,
    string? ModelPackageKey);

public sealed record UploadImportFileResponse(
    ImportBatchResponse Batch,
    ImportFileEvidenceResponse Evidence);

public sealed record ImportBatchResponse(
    Guid Id,
    Guid TenantId,
    string SourceSystem,
    string? Description,
    ImportBatchStatus Status,
    Guid ActiveModelPackageVersionId,
    string? ActiveModelPackageKey,
    string? ActiveModelPackageVersionLabel,
    int EvidenceCount,
    int MappingVersionCount,
    int ValidationIssueCount,
    int StagingRunCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? StagedAt);

public sealed record ImportBatchDetailResponse(
    ImportBatchResponse Batch,
    IReadOnlyCollection<ImportFileEvidenceResponse> Evidence,
    IReadOnlyCollection<ImportMappingVersionResponse> MappingVersions,
    IReadOnlyCollection<ImportValidationIssueResponse> ValidationIssues,
    IReadOnlyCollection<ImportStagingGraphRunResponse> StagingRuns);

public sealed record ImportFileEvidenceResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    string StorageKey,
    string Sha256Checksum,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt);

public sealed record ImportPreviewRequest(
    Guid? EvidenceId,
    int SampleRowLimit);

public sealed record ImportPreviewResponse(
    Guid BatchId,
    Guid EvidenceId,
    Guid ActiveModelPackageVersionId,
    string ActiveModelPackageKey,
    string ActiveModelPackageVersionLabel,
    string SuggestionProvider,
    IReadOnlyCollection<string> Headers,
    IReadOnlyCollection<IReadOnlyDictionary<string, string?>> SampleRows,
    IReadOnlyCollection<ImportColumnMappingSuggestionResponse> ColumnSuggestions,
    IReadOnlyCollection<ImportLifecycleMappingSuggestionResponse> LifecycleSuggestions);

public sealed record ImportColumnMappingSuggestionResponse(
    string SourceColumn,
    string CanonicalObjectType,
    string? CanonicalAttributeKey,
    bool IsIdentityField,
    bool IsRequired,
    decimal Confidence,
    string Rationale);

public sealed record ImportLifecycleMappingSuggestionResponse(
    string SourceValue,
    string CanonicalLifecycleKey,
    decimal Confidence,
    string Rationale);

public sealed record CreateImportMappingVersionRequest(
    Guid ImportBatchId,
    string VersionLabel,
    string? Summary,
    IReadOnlyCollection<CreateImportColumnMappingRequest> ColumnMappings,
    IReadOnlyCollection<CreateImportLifecycleMappingRequest> LifecycleMappings);

public sealed record CreateImportColumnMappingRequest(
    string SourceColumn,
    string CanonicalObjectType,
    string? CanonicalAttributeKey,
    bool IsIdentityField,
    bool IsRequired);

public sealed record CreateImportLifecycleMappingRequest(
    string SourceValue,
    string CanonicalLifecycleKey);

public sealed record ApproveImportMappingRequest(string? Summary);

public sealed record ImportMappingVersionResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid ModelPackageVersionId,
    string VersionLabel,
    string? Summary,
    ImportMappingState State,
    string SuggestionProvider,
    int ColumnMappingCount,
    int LifecycleMappingCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    Guid? RejectedByUserId,
    DateTimeOffset? RejectedAt,
    IReadOnlyCollection<ImportColumnMappingResponse> ColumnMappings,
    IReadOnlyCollection<ImportLifecycleMappingResponse> LifecycleMappings);

public sealed record ImportColumnMappingResponse(
    Guid Id,
    string SourceColumn,
    string CanonicalObjectType,
    string? CanonicalAttributeKey,
    bool IsIdentityField,
    bool IsRequired);

public sealed record ImportLifecycleMappingResponse(
    Guid Id,
    string SourceValue,
    string CanonicalLifecycleKey);

public sealed record ImportValidationResponse(
    Guid BatchId,
    Guid MappingVersionId,
    bool IsValid,
    int ErrorCount,
    int WarningCount,
    IReadOnlyCollection<ImportValidationIssueResponse> Issues);

public sealed record ImportValidationIssueResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid? ImportMappingVersionId,
    ImportIssueSeverity Severity,
    int? RowNumber,
    string? SourceColumn,
    string? CanonicalObjectType,
    string IssueCode,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record ImportStagingGraphRunResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid ImportMappingVersionId,
    ImportStagingRunStatus Status,
    int NodeCount,
    int RelationshipCount,
    IReadOnlyCollection<Guid> GraphNodeIds,
    IReadOnlyCollection<Guid> GraphRelationshipIds,
    string? FailureSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ImportPromotionRunResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid ImportStagingGraphRunId,
    ImportPromotionRunStatus Status,
    int PromotedNodeCount,
    int PromotedRelationshipCount,
    IReadOnlyCollection<Guid> SourceEvidenceIds,
    Guid? AuditRecordId,
    string? FailureSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record RejectedStagingSummaryResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    Guid ImportStagingGraphRunId,
    string ValidationSummaryJson,
    string DecisionSummaryJson,
    int NodeCount,
    int RelationshipCount,
    IReadOnlyCollection<Guid> SourceEvidenceIds,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt);

public sealed record BomComparisonRunResponse(
    Guid Id,
    Guid TenantId,
    Guid ImportBatchId,
    string? SourceContext,
    string CadSummaryJson,
    string EbomSummaryJson,
    int MissingInCadCount,
    int MissingInEbomCount,
    int QuantityMismatchCount,
    int UsageReferenceMismatchCount,
    int UnresolvedIdentityCount,
    string ResultJson,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt);
