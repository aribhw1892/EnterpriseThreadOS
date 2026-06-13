namespace ETOS.Backend.AiTrace;

public static class AiTracePermissions
{
    public const string Read = "ai_trace.read";
    public const string Export = "ai_trace.export";
    public const string Admin = "ai_trace.admin";
}

public sealed record AiTraceSummaryResponse(
    Guid Id,
    Guid TenantId,
    AiTraceKind TraceKind,
    string IntentKey,
    string StrategyKey,
    string Status,
    string SafeSummary,
    Guid RequestedByUserId,
    DateTimeOffset CreatedAt);

public sealed record AiTraceSourceSummaryResponse(
    string SourceKind,
    int Count,
    IReadOnlyCollection<string> SafeReferences);

public sealed record AiTraceConfidenceImpactResponse(
    int RetrievedCount,
    int FilteredCount,
    int DeniedCount,
    int TrustFilteredCount,
    string? PolicyKey,
    string Notes);

public sealed record AiTraceArtifactLinkResponse(
    Guid Id,
    AiTraceArtifactLinkKind LinkKind,
    string ObjectType,
    string ObjectId);

public sealed record AiTraceDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid RetrievalRunId,
    Guid ContextPackageId,
    Guid? AuditRecordId,
    AiTraceKind TraceKind,
    string IntentKey,
    string StrategyKey,
    string QueryText,
    string Status,
    string SafeSummary,
    IReadOnlyCollection<AiTraceSourceSummaryResponse> SourcesSummary,
    IReadOnlyCollection<TraceContextSummaryResponse> FilteredSummaries,
    IReadOnlyCollection<TraceDeniedSummaryResponse> DeniedSafeSummaries,
    IReadOnlyCollection<TraceSensitiveDeniedReferenceResponse>? SensitiveDeniedReferences,
    AiTraceConfidenceImpactResponse ConfidenceImpact,
    string? PromptTemplateVersionLabel,
    string? OutputSchemaVersionLabel,
    string? GeneratedOutputJson,
    IReadOnlyCollection<AiTraceArtifactLinkResponse> ArtifactLinks,
    Guid RequestedByUserId,
    DateTimeOffset CreatedAt);

public sealed record TraceContextSummaryResponse(
    string ContextId,
    string ContextType,
    string SourceKind,
    string SafeSummary);

public sealed record TraceDeniedSummaryResponse(
    string ContextId,
    string ContextType,
    string SafeSummary,
    string Reason);

public sealed record TraceSensitiveDeniedReferenceResponse(
    string ContextId,
    string ContextType,
    string? DocumentId,
    string ClassificationKey,
    string? AttributeKey,
    string Reason);

public sealed record AiTraceExportResponse(
    Guid ExportRecordId,
    Guid AiTraceRecordId,
    string ExportHash,
    string EvidenceLevel,
    AiTraceExportRedactionMetadataResponse RedactionMetadata,
    DateTimeOffset ExportedAt);

public sealed record AiTraceExportRedactionMetadataResponse(
    string? PolicyKey,
    string? PolicyVersion,
    IReadOnlyCollection<string> RedactedCategories,
    string EvidenceLevel,
    DateTimeOffset ExportedAt,
    Guid ExportedByUserId);

public sealed record AiTraceExportFileResult(
    byte[] Content,
    string FileName,
    string ContentType,
    AiTraceExportResponse Metadata);
