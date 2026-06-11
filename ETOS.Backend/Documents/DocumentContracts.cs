namespace ETOS.Backend.Documents;

public static class DocumentPermissions
{
    public const string Read = "documents.read";
    public const string Manage = "documents.manage";
    public const string Link = "documents.link";
    public const string Index = "documents.index";
    public const string Admin = "documents.admin";
}

public sealed record CreateDocumentArtifactRequest(
    string DocumentType,
    string ClassificationKey,
    string Title,
    string? Description,
    Guid? OwnerUserId);

public sealed record CreateDocumentVersionRequest(
    string VersionLabel,
    string? ExtractedMetadataSummaryJson,
    DocumentExtractionStatus ExtractionStatus,
    string? ExtractionFailureSummary);

public sealed record CreateDocumentObjectLinkRequest(
    Guid DocumentVersionId,
    Guid? GraphNodeId,
    Guid? ImportBatchId,
    decimal ConfidenceScore,
    string EvidenceSummary,
    DocumentExtractionStatus ExtractionStatus,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record CreateDocumentExtractionIssueRequest(
    string Title,
    string IssueCode,
    string EvidenceSummary,
    string? Rationale);

public sealed record CreateDocumentVectorIndexRequest(
    string? PolicyKey,
    string? SafeSummary);

public sealed record DocumentArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    Guid ArtifactId,
    string DocumentType,
    string ClassificationKey,
    string Title,
    string? Description,
    Guid OwnerUserId,
    DocumentVersionResponse? LatestVersion,
    int LinkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DocumentArtifactDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid ArtifactId,
    string DocumentType,
    string ClassificationKey,
    string Title,
    string? Description,
    Guid OwnerUserId,
    IReadOnlyCollection<DocumentVersionResponse> Versions,
    IReadOnlyCollection<DocumentObjectLinkResponse> ObjectLinks,
    IReadOnlyCollection<DocumentVectorIndexRecordResponse> VectorIndexRecords,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DocumentVersionResponse(
    Guid Id,
    Guid TenantId,
    Guid DocumentArtifactId,
    string VersionLabel,
    string StorageKey,
    string Sha256Checksum,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? ExtractedMetadataSummaryJson,
    DocumentExtractionStatus ExtractionStatus,
    string? ExtractionFailureSummary,
    Guid UploadedByUserId,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt);

public sealed record DocumentObjectLinkResponse(
    Guid Id,
    Guid TenantId,
    Guid DocumentArtifactId,
    Guid DocumentVersionId,
    Guid? GraphNodeId,
    Guid? ImportBatchId,
    decimal ConfidenceScore,
    string EvidenceSummary,
    DocumentExtractionStatus ExtractionStatus,
    string? SourceSystem,
    string? SourceRecordId,
    Guid CreatedByUserId,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt);

public sealed record DocumentVectorIndexRecordResponse(
    Guid Id,
    Guid TenantId,
    Guid DocumentArtifactId,
    Guid DocumentVersionId,
    string ProviderName,
    DocumentVectorIndexStatus Status,
    string TenantFilter,
    string PolicyFilterSummary,
    string SafeSummary,
    string? FailureSummary,
    Guid RequestedByUserId,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt);

public sealed record CadParsingPlaceholderResponse(
    bool IsEnabled,
    string ProviderName,
    string SafeSummary);
