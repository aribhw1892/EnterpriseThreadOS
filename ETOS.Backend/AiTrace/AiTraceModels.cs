using System.Text.Json.Serialization;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.AiTrace;

public sealed class AiTraceRecord : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RetrievalRunId { get; set; }
    public Guid ContextPackageId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public AiTraceKind TraceKind { get; set; } = AiTraceKind.GovernedQuery;
    public required string IntentKey { get; set; }
    public required string StrategyKey { get; set; }
    public required string QueryText { get; set; }
    public required string Status { get; set; }
    public required string SafeSummary { get; set; }
    public required string SourcesSummaryJson { get; set; }
    public required string FilteredSummariesJson { get; set; }
    public required string DeniedSafeSummariesJson { get; set; }
    public required string SensitiveDeniedReferencesJson { get; set; }
    public required string ConfidenceImpactJson { get; set; }
    public string? PromptTemplateVersionLabel { get; set; }
    public string? OutputSchemaVersionLabel { get; set; }
    public string? GeneratedOutputJson { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<AiTraceArtifactLink> ArtifactLinks { get; set; } = [];
    public List<AiTraceExportRecord> ExportRecords { get; set; } = [];
}

public sealed class AiTraceArtifactLink : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AiTraceRecordId { get; set; }
    public AiTraceRecord? AiTraceRecord { get; set; }
    public AiTraceArtifactLinkKind LinkKind { get; set; }
    public required string ObjectType { get; set; }
    public required string ObjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AiTraceExportRecord : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AiTraceRecordId { get; set; }
    public AiTraceRecord? AiTraceRecord { get; set; }
    public Guid ExportedByUserId { get; set; }
    public required string ExportHash { get; set; }
    public required string RedactionMetadataJson { get; set; }
    public required string EvidenceLevel { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiTraceKind
{
    GovernedQuery = 0
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiTraceArtifactLinkKind
{
    QueryIntent = 0,
    RetrievalStrategy = 1,
    DocumentArtifact = 2,
    GraphNode = 3,
    ContextPackage = 4,
    RetrievalRun = 5
}
