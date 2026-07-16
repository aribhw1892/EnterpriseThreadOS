namespace ETOS.Backend.DigitalThread;

public static class DigitalThreadPermissions
{
    public const string Read = "digital_thread.read";
    public const string Admin = "digital_thread.admin";
}

public sealed record DigitalThreadTopThreadResponse(
    string Id,
    string Label,
    int EventCount);

public sealed record DigitalThreadHeatmapBucketResponse(
    string SystemId,
    DateTimeOffset BucketStartUtc,
    int EventCount);

public sealed record DigitalThreadOpenAlertCountsResponse(
    int DataQualityOpen,
    int SecurityHighOrCritical,
    int FailedRuns,
    int Total);

public sealed record DigitalThreadSummaryResponse(
    int ConnectedSystemCount,
    int HealthySystemCount,
    int WarningSystemCount,
    int DownSystemCount,
    double EventsLastMinute,
    DigitalThreadOpenAlertCountsResponse OpenAlertCounts,
    IReadOnlyCollection<DigitalThreadTopThreadResponse> TopThreads,
    IReadOnlyCollection<DigitalThreadHeatmapBucketResponse> HeatmapBuckets,
    int WindowHours,
    DateTimeOffset GeneratedAtUtc);

public sealed record DigitalThreadSystemResponse(
    string SystemId,
    string DisplayName,
    string SystemType,
    string ConnectionStatus,
    DateTimeOffset? LastEventAtUtc,
    int EventCount24h,
    string SyncStatus);

public sealed record DigitalThreadEventResponse(
    string EventId,
    DateTimeOffset TimestampUtc,
    string SourceSystemId,
    string SourceSystemName,
    string EventType,
    string Title,
    string Description,
    Guid? ArtifactId,
    string TrustState,
    string SyncStatus,
    string? Severity,
    Guid? TraceId,
    Guid? RecommendationId);

public sealed record DigitalThreadProjectionPointResponse(
    double X,
    double Y);

public sealed record DigitalThreadBranchResponse(
    string BranchId,
    IReadOnlyCollection<string> SystemIds,
    DateTimeOffset TimeStartUtc,
    DateTimeOffset TimeEndUtc,
    int EventCount,
    string Health,
    double? TrustScore,
    IReadOnlyCollection<DigitalThreadProjectionPointResponse> ProjectionPoints);

public sealed record DigitalThreadLineageHopResponse(
    Guid FromArtifactId,
    string FromLabel,
    Guid ToArtifactId,
    string ToLabel,
    string RelationshipType,
    string TrustState);

public sealed record DigitalThreadLineageResponse(
    Guid ArtifactId,
    string Label,
    IReadOnlyCollection<DigitalThreadLineageHopResponse> Hops,
    IReadOnlyCollection<DigitalThreadEventResponse> RelatedEvents);

public sealed record DigitalThreadEvidenceLinkResponse(
    string LinkType,
    string Label,
    string? Href,
    string? SafeSummary);

public sealed record DigitalThreadDrillRouteResponse(
    string RouteType,
    string Label,
    string Href);

public sealed record DigitalThreadEventDetailResponse(
    string EventId,
    DateTimeOffset TimestampUtc,
    string SourceSystemId,
    string SourceSystemName,
    string EventType,
    string Title,
    string Description,
    Guid? ArtifactId,
    string TrustState,
    string SyncStatus,
    string? Severity,
    Guid? TraceId,
    Guid? RecommendationId,
    string? PolicySafeSummary,
    string? DataQualitySafeSummary,
    IReadOnlyCollection<DigitalThreadEvidenceLinkResponse> EvidenceLinks,
    IReadOnlyCollection<DigitalThreadDrillRouteResponse> DrillRoutes);

public sealed record DigitalThreadMinimapSystemResponse(
    string SystemId,
    string DisplayName,
    string ConnectionStatus,
    double X,
    double Y);

public sealed record DigitalThreadMinimapResponse(
    int WindowHours,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    IReadOnlyCollection<DigitalThreadMinimapSystemResponse> Systems,
    IReadOnlyCollection<DigitalThreadProjectionPointResponse> CoarsePoints);

public sealed record DigitalThreadStreamEnvelope(
    string Cursor,
    DigitalThreadEventResponse? Event,
    bool Heartbeat = false);

/// <summary>
/// UI switch for preview fixtures vs live projection APIs.
/// Sourced from <c>DigitalThread:UseLiveProjection</c> in appsettings.
/// </summary>
public sealed record DigitalThreadSettingsResponse(
    bool UseLiveProjection);
