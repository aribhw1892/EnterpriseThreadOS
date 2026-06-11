using System.Text.Json.Serialization;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Imports;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Documents;

public sealed class DocumentArtifact : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactId { get; set; }
    public Artifact? Artifact { get; set; }
    public required string DocumentType { get; set; }
    public required string NormalizedDocumentType { get; set; }
    public required string ClassificationKey { get; set; }
    public required string NormalizedClassificationKey { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DocumentVersion> Versions { get; set; } = [];
    public List<DocumentObjectLink> ObjectLinks { get; set; } = [];
}

public sealed class DocumentVersion : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentArtifactId { get; set; }
    public DocumentArtifact? DocumentArtifact { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public required string StorageKey { get; set; }
    public required string Sha256Checksum { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? ExtractedMetadataSummaryJson { get; set; }
    public DocumentExtractionStatus ExtractionStatus { get; set; } = DocumentExtractionStatus.NotStarted;
    public string? ExtractionFailureSummary { get; set; }
    public Guid UploadedByUserId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DocumentObjectLink> ObjectLinks { get; set; } = [];
    public List<DocumentVectorIndexRecord> VectorIndexRecords { get; set; } = [];
}

public sealed class DocumentObjectLink : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentArtifactId { get; set; }
    public DocumentArtifact? DocumentArtifact { get; set; }
    public Guid DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }
    public Guid? GraphNodeId { get; set; }
    public Guid? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public decimal ConfidenceScore { get; set; }
    public required string EvidenceSummary { get; set; }
    public DocumentExtractionStatus ExtractionStatus { get; set; } = DocumentExtractionStatus.NotStarted;
    public string? SourceSystem { get; set; }
    public string? SourceRecordId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentVectorIndexRecord : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentArtifactId { get; set; }
    public DocumentArtifact? DocumentArtifact { get; set; }
    public Guid DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }
    public required string ProviderName { get; set; }
    public DocumentVectorIndexStatus Status { get; set; } = DocumentVectorIndexStatus.Pending;
    public required string TenantFilter { get; set; }
    public required string PolicyFilterSummary { get; set; }
    public required string SafeSummary { get; set; }
    public string? FailureSummary { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentExtractionStatus
{
    NotStarted = 0,
    MetadataImported = 1,
    Completed = 2,
    Failed = 3,
    Uncertain = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentVectorIndexStatus
{
    Pending = 0,
    Indexed = 1,
    Failed = 2,
    DisabledPlaceholder = 3
}
