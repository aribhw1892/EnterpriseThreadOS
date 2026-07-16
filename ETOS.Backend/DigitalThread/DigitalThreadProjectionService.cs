using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.DigitalThread;

public interface IDigitalThreadProjectionService
{
    Task<DigitalThreadSummaryResponse> GetSummaryAsync(int? windowHours, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DigitalThreadSystemResponse>> ListSystemsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DigitalThreadEventResponse>> ListEventsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? systemId,
        int? limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DigitalThreadBranchResponse>> ListBranchesAsync(
        int? windowHours,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<DigitalThreadLineageResponse?> GetLineageAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<DigitalThreadEventDetailResponse?> GetEventDetailAsync(string eventId, CancellationToken cancellationToken);

    Task<DigitalThreadMinimapResponse> GetMinimapAsync(int? windowHours, CancellationToken cancellationToken);

    IAsyncEnumerable<DigitalThreadStreamEnvelope> StreamEventsAsync(
        DateTimeOffset? since,
        string? sinceEventId,
        CancellationToken cancellationToken);
}

public sealed class DigitalThreadProjectionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService) : IDigitalThreadProjectionService
{
    private const int DefaultWindowHours = 24;
    private const int MaxWindowHours = 168;
    private const int DefaultEventLimit = 50;
    private const int MaxEventLimit = 200;
    private const int EventsPerMinuteWindowMinutes = 5;
    private const int HeatmapBucketHours = 2;
    private const int SourceQueryTake = 200;
    private const int StreamPollSeconds = 3;
    private const int StreamBatchTake = 40;
    private const double CanvasWidth = 1000d;
    private const double CanvasHeight = 420d;

    private static readonly string RecommendationType =
        RecommendationArtifactTypes.Recommendation.ToUpperInvariant();

    private static readonly string ConnectorType =
        ConnectorDefinitionArtifactTypes.ConnectorDefinition.ToUpperInvariant();

    public async Task<DigitalThreadSummaryResponse> GetSummaryAsync(
        int? windowHours,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.summary", cancellationToken);
        var resolvedHours = ResolveWindowHours(windowHours);
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddHours(-resolvedHours);
        var events = await CollectEventsAsync(context.TenantId, windowStart, now, null, SourceQueryTake, cancellationToken);
        var systems = await BuildSystemsAsync(context.TenantId, now, events, cancellationToken);

        var rateWindowStart = now.AddMinutes(-EventsPerMinuteWindowMinutes);
        var recentCount = events.Count(item => item.TimestampUtc >= rateWindowStart);
        var eventsLastMinute = recentCount / (double)EventsPerMinuteWindowMinutes;

        var openDq = await dbContext.DataQualityIssues
            .AsNoTracking()
            .CountAsync(
                issue => issue.TenantId == context.TenantId
                    && (issue.Status == DataQualityIssueStatus.Open
                        || issue.Status == DataQualityIssueStatus.Acknowledged),
                cancellationToken);

        var securityHigh = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(
                item => item.TenantId == context.TenantId
                    && (item.Severity == SecurityEventSeverity.High
                        || item.Severity == SecurityEventSeverity.Critical),
                cancellationToken);

        var failedRuns = events.Count(item =>
            item.SyncStatus.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Severity, "high", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Severity, "critical", StringComparison.OrdinalIgnoreCase));

        var topThreads = events
            .GroupBy(item => item.SourceSystemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DigitalThreadTopThreadResponse(
                group.Key,
                group.First().SourceSystemName,
                group.Count()))
            .OrderByDescending(item => item.EventCount)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var heatmap = BuildHeatmap(events, windowStart, now);

        return new DigitalThreadSummaryResponse(
            systems.Count,
            systems.Count(item => item.ConnectionStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase)),
            systems.Count(item => item.ConnectionStatus.Equals("Warning", StringComparison.OrdinalIgnoreCase)),
            systems.Count(item => item.ConnectionStatus.Equals("Down", StringComparison.OrdinalIgnoreCase)),
            Math.Round(eventsLastMinute, 2),
            new DigitalThreadOpenAlertCountsResponse(
                openDq,
                securityHigh,
                failedRuns,
                openDq + securityHigh + failedRuns),
            topThreads,
            heatmap,
            resolvedHours,
            now);
    }

    public async Task<IReadOnlyCollection<DigitalThreadSystemResponse>> ListSystemsAsync(
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.systems", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddHours(-DefaultWindowHours);
        var events = await CollectEventsAsync(context.TenantId, windowStart, now, null, SourceQueryTake, cancellationToken);
        return await BuildSystemsAsync(context.TenantId, now, events, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DigitalThreadEventResponse>> ListEventsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? systemId,
        int? limit,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.events", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var windowEnd = to ?? now;
        var windowStart = from ?? windowEnd.AddHours(-DefaultWindowHours);
        if (windowStart > windowEnd)
        {
            throw new RequestValidationException("'from' must be earlier than or equal to 'to'.");
        }

        var resolvedLimit = ResolveEventLimit(limit);
        var events = await CollectEventsAsync(
            context.TenantId,
            windowStart,
            windowEnd,
            systemId,
            resolvedLimit,
            cancellationToken);

        return events
            .OrderByDescending(item => item.TimestampUtc)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .Take(resolvedLimit)
            .ToList();
    }

    public async Task<IReadOnlyCollection<DigitalThreadBranchResponse>> ListBranchesAsync(
        int? windowHours,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.branches", cancellationToken);
        var (windowStart, windowEnd, _) = ResolveWindow(windowHours, from, to);
        var events = await CollectEventsAsync(
            context.TenantId,
            windowStart,
            windowEnd,
            null,
            SourceQueryTake,
            cancellationToken);
        var systems = await BuildSystemsAsync(context.TenantId, windowEnd, events, cancellationToken);
        return BuildBranches(events, systems, windowStart, windowEnd);
    }

    public async Task<DigitalThreadLineageResponse?> GetLineageAsync(
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.lineage", cancellationToken);
        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.Id == artifactId)
            .Select(item => new { item.Id, item.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        var relationships = await dbContext.ArtifactRelationships
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && (item.SourceArtifactId == artifactId || item.TargetArtifactId == artifactId))
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                item.SourceArtifactId,
                item.TargetArtifactId,
                item.RelationshipType,
                item.Description
            })
            .ToListAsync(cancellationToken);

        var relatedIds = relationships
            .SelectMany(item => new[] { item.SourceArtifactId, item.TargetArtifactId })
            .Distinct()
            .ToArray();
        var labels = relatedIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Artifacts
                .AsNoTracking()
                .Where(item => item.TenantId == context.TenantId && relatedIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var hops = relationships
            .Select(item => new DigitalThreadLineageHopResponse(
                item.SourceArtifactId,
                labels.GetValueOrDefault(item.SourceArtifactId, item.SourceArtifactId.ToString("N")[..8]),
                item.TargetArtifactId,
                labels.GetValueOrDefault(item.TargetArtifactId, item.TargetArtifactId.ToString("N")[..8]),
                item.RelationshipType.ToString(),
                "Unverified"))
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var events = await CollectEventsAsync(
            context.TenantId,
            now.AddHours(-DefaultWindowHours),
            now,
            null,
            SourceQueryTake,
            cancellationToken);
        var relatedEvents = events
            .Where(item => item.ArtifactId == artifactId
                || item.RecommendationId == artifactId
                || (item.ArtifactId is not null && relatedIds.Contains(item.ArtifactId.Value)))
            .OrderByDescending(item => item.TimestampUtc)
            .Take(40)
            .ToList();

        return new DigitalThreadLineageResponse(artifact.Id, artifact.Name, hops, relatedEvents);
    }

    public async Task<DigitalThreadEventDetailResponse?> GetEventDetailAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.event-detail", cancellationToken);
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new RequestValidationException("eventId is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var events = await CollectEventsAsync(
            context.TenantId,
            now.AddHours(-MaxWindowHours),
            now,
            null,
            SourceQueryTake,
            cancellationToken);
        var match = events.FirstOrDefault(item =>
            item.EventId.Equals(eventId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            match = await ResolveEventByIdAsync(context.TenantId, eventId.Trim(), cancellationToken);
        }

        if (match is null)
        {
            return null;
        }

        string? policySafeSummary = null;
        string? dataQualitySafeSummary = null;
        var evidence = new List<DigitalThreadEvidenceLinkResponse>();
        var drills = new List<DigitalThreadDrillRouteResponse>();

        if (match.ArtifactId is Guid artifactId)
        {
            drills.Add(new DigitalThreadDrillRouteResponse(
                "artifact",
                "Open artifact",
                $"/artifacts/{artifactId}"));
            drills.Add(new DigitalThreadDrillRouteResponse(
                "explorer360",
                "Open 360° context",
                $"/explorers/360/{artifactId}"));
            evidence.Add(new DigitalThreadEvidenceLinkResponse(
                "artifact",
                "Linked artifact",
                $"/artifacts/{artifactId}",
                match.Description));
        }

        if (match.TraceId is Guid traceId)
        {
            drills.Add(new DigitalThreadDrillRouteResponse(
                "ai-trace",
                "Open AI Trace",
                $"/ai-traces/{traceId}"));
            evidence.Add(new DigitalThreadEvidenceLinkResponse(
                "ai-trace",
                "AI Trace record",
                $"/ai-traces/{traceId}",
                null));
        }

        if (match.RecommendationId is Guid recommendationId)
        {
            drills.Add(new DigitalThreadDrillRouteResponse(
                "recommendation",
                "Open recommendation",
                $"/recommendations/{recommendationId}"));
        }

        if (eventId.StartsWith("dq:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(eventId["dq:".Length..], out var dqId))
        {
            var issue = await dbContext.DataQualityIssues
                .AsNoTracking()
                .Where(item => item.TenantId == context.TenantId && item.Id == dqId)
                .Select(item => new { item.EvidenceSummary, item.Title })
                .FirstOrDefaultAsync(cancellationToken);
            if (issue is not null)
            {
                dataQualitySafeSummary = issue.EvidenceSummary;
                evidence.Add(new DigitalThreadEvidenceLinkResponse(
                    "data-quality",
                    issue.Title,
                    null,
                    issue.EvidenceSummary));
            }
        }

        if (eventId.StartsWith("audit:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(eventId["audit:".Length..], out var auditId))
        {
            var audit = await dbContext.AuditRecords
                .AsNoTracking()
                .Where(item => item.TenantId == context.TenantId && item.Id == auditId)
                .Select(item => item.SafeSummary)
                .FirstOrDefaultAsync(cancellationToken);
            policySafeSummary = audit;
        }

        if (eventId.StartsWith("security:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(eventId["security:".Length..], out var securityId))
        {
            var security = await dbContext.SecurityEvents
                .AsNoTracking()
                .Where(item => item.TenantId == context.TenantId && item.Id == securityId)
                .Select(item => item.SafeSummary)
                .FirstOrDefaultAsync(cancellationToken);
            policySafeSummary ??= security;
        }

        return new DigitalThreadEventDetailResponse(
            match.EventId,
            match.TimestampUtc,
            match.SourceSystemId,
            match.SourceSystemName,
            match.EventType,
            match.Title,
            match.Description,
            match.ArtifactId,
            match.TrustState,
            match.SyncStatus,
            match.Severity,
            match.TraceId,
            match.RecommendationId,
            policySafeSummary,
            dataQualitySafeSummary,
            evidence,
            drills);
    }

    public async Task<DigitalThreadMinimapResponse> GetMinimapAsync(
        int? windowHours,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.minimap", cancellationToken);
        var (windowStart, windowEnd, resolvedHours) = ResolveWindow(windowHours, null, null);
        var events = await CollectEventsAsync(
            context.TenantId,
            windowStart,
            windowEnd,
            null,
            SourceQueryTake,
            cancellationToken);
        var systems = await BuildSystemsAsync(context.TenantId, windowEnd, events, cancellationToken);
        var branches = BuildBranches(events, systems, windowStart, windowEnd);

        var orderedSystems = systems
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
        var systemPoints = orderedSystems
            .Select((system, index) =>
            {
                var (x, y) = SystemLayoutPoint(index, orderedSystems.Count);
                return new DigitalThreadMinimapSystemResponse(
                    system.SystemId,
                    system.DisplayName,
                    system.ConnectionStatus,
                    x,
                    y);
            })
            .ToList();

        var coarsePoints = branches
            .SelectMany(branch => branch.ProjectionPoints)
            .Where((_, index) => index % 3 == 0)
            .Take(120)
            .ToList();

        if (coarsePoints.Count == 0 && systemPoints.Count > 0)
        {
            coarsePoints = systemPoints
                .Select(item => new DigitalThreadProjectionPointResponse(item.X, item.Y))
                .ToList();
        }

        return new DigitalThreadMinimapResponse(
            resolvedHours,
            windowStart,
            windowEnd,
            systemPoints,
            coarsePoints);
    }

    public async IAsyncEnumerable<DigitalThreadStreamEnvelope> StreamEventsAsync(
        DateTimeOffset? since,
        string? sinceEventId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("digital-thread.events.stream", cancellationToken);
        var cursorTime = since ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        var cursorEventId = sinceEventId;

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var events = await CollectEventsAsync(
                context.TenantId,
                cursorTime.AddSeconds(-1),
                now,
                null,
                StreamBatchTake,
                cancellationToken);

            var fresh = events
                .Where(item =>
                    item.TimestampUtc > cursorTime
                    || (item.TimestampUtc == cursorTime
                        && (cursorEventId is null
                            || string.CompareOrdinal(item.EventId, cursorEventId) > 0)))
                .OrderBy(item => item.TimestampUtc)
                .ThenBy(item => item.EventId, StringComparer.Ordinal)
                .Take(StreamBatchTake)
                .ToList();

            foreach (var item in fresh)
            {
                cursorTime = item.TimestampUtc;
                cursorEventId = item.EventId;
                yield return new DigitalThreadStreamEnvelope(
                    $"{item.TimestampUtc:O}|{item.EventId}",
                    item);
            }

            yield return new DigitalThreadStreamEnvelope(
                $"{cursorTime:O}|{cursorEventId ?? "heartbeat"}",
                null,
                Heartbeat: true);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(StreamPollSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private async Task<IReadOnlyList<DigitalThreadSystemResponse>> BuildSystemsAsync(
        Guid tenantId,
        DateTimeOffset now,
        IReadOnlyList<DigitalThreadEventResponse> windowEvents,
        CancellationToken cancellationToken)
    {
        var windowStart = now.AddHours(-DefaultWindowHours);
        var systems = new Dictionary<string, DigitalThreadSystemResponse>(StringComparer.OrdinalIgnoreCase);

        var connectors = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.NormalizedArtifactType == ConnectorType)
            .OrderByDescending(item => item.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var connectorIds = connectors.Select(item => item.Id).ToArray();
        var latestVersions = connectorIds.Length == 0
            ? []
            : await dbContext.ArtifactVersions
                .AsNoTracking()
                .Where(version => connectorIds.Contains(version.ArtifactId))
                .GroupBy(version => version.ArtifactId)
                .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
                .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        foreach (var artifact in connectors)
        {
            versionLookup.TryGetValue(artifact.Id, out var version);
            string connectorKey = artifact.Name;
            string connectorKind = "Connector";
            var executionEnabled = false;
            if (version?.PayloadJson is not null)
            {
                var payload = ConnectorDefinitionPayloadParser.Deserialize(version.PayloadJson);
                if (!string.IsNullOrWhiteSpace(payload.ConnectorKey))
                {
                    connectorKey = payload.ConnectorKey;
                }

                if (!string.IsNullOrWhiteSpace(payload.ConnectorKind))
                {
                    connectorKind = payload.ConnectorKind;
                }

                executionEnabled = payload.ExecutionEnabled;
            }

            var systemId = NormalizeSystemId(connectorKey);
            var connectionStatus = executionEnabled ? "Healthy" : "Warning";
            systems[systemId] = new DigitalThreadSystemResponse(
                systemId,
                artifact.Name,
                connectorKind,
                connectionStatus,
                artifact.UpdatedAt,
                0,
                executionEnabled ? "OK" : "Warning");
        }

        var importBatches = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(batch => batch.TenantId == tenantId)
            .OrderByDescending(batch => batch.CreatedAt)
            .Take(SourceQueryTake)
            .Select(batch => new
            {
                batch.SourceSystem,
                batch.NormalizedSourceSystem,
                batch.Status,
                batch.CreatedAt,
                batch.StagedAt,
                batch.ValidatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var group in importBatches.GroupBy(item => item.NormalizedSourceSystem, StringComparer.OrdinalIgnoreCase))
        {
            var latest = group.OrderByDescending(item => item.CreatedAt).First();
            var systemId = NormalizeSystemId(group.Key);
            var eventCount24h = group.Count(item =>
                item.CreatedAt >= windowStart
                || (item.StagedAt is not null && item.StagedAt >= windowStart)
                || (item.ValidatedAt is not null && item.ValidatedAt >= windowStart));
            var hasFailed = group.Any(item => item.Status == ImportBatchStatus.Failed);
            var connectionStatus = hasFailed ? "Warning" : "Healthy";
            var syncStatus = hasFailed ? "Warning" : "OK";
            var lastEvent = group
                .SelectMany(item => new DateTimeOffset?[] { item.CreatedAt, item.ValidatedAt, item.StagedAt })
                .Where(stamp => stamp is not null)
                .Select(stamp => stamp!.Value)
                .DefaultIfEmpty(latest.CreatedAt)
                .Max();

            if (systems.TryGetValue(systemId, out var existing))
            {
                systems[systemId] = existing with
                {
                    LastEventAtUtc = MaxNullable(existing.LastEventAtUtc, lastEvent),
                    EventCount24h = existing.EventCount24h + eventCount24h,
                    ConnectionStatus = PreferWorseStatus(existing.ConnectionStatus, connectionStatus),
                    SyncStatus = PreferWorseSync(existing.SyncStatus, syncStatus)
                };
            }
            else
            {
                systems[systemId] = new DigitalThreadSystemResponse(
                    systemId,
                    latest.SourceSystem,
                    "ImportSource",
                    connectionStatus,
                    lastEvent,
                    eventCount24h,
                    syncStatus);
            }
        }

        foreach (var group in windowEvents.GroupBy(item => item.SourceSystemId, StringComparer.OrdinalIgnoreCase))
        {
            if (systems.ContainsKey(group.Key))
            {
                var existing = systems[group.Key];
                systems[group.Key] = existing with
                {
                    EventCount24h = Math.Max(existing.EventCount24h, group.Count()),
                    LastEventAtUtc = MaxNullable(existing.LastEventAtUtc, group.Max(item => item.TimestampUtc))
                };
                continue;
            }

            var sample = group.First();
            var hasError = group.Any(item => item.SyncStatus.Equals("Error", StringComparison.OrdinalIgnoreCase));
            var hasWarning = group.Any(item => item.SyncStatus.Equals("Warning", StringComparison.OrdinalIgnoreCase));
            systems[group.Key] = new DigitalThreadSystemResponse(
                group.Key,
                sample.SourceSystemName,
                InferSystemType(group.Key),
                hasError ? "Down" : hasWarning ? "Warning" : "Healthy",
                group.Max(item => item.TimestampUtc),
                group.Count(),
                hasError ? "Error" : hasWarning ? "Warning" : "OK");
        }

        return systems.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<DigitalThreadEventResponse>> CollectEventsAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? systemId,
        int takeHint,
        CancellationToken cancellationToken)
    {
        var events = new List<DigitalThreadEventResponse>(takeHint * 2);
        var normalizedSystem = string.IsNullOrWhiteSpace(systemId) ? null : NormalizeSystemId(systemId);

        var importBatches = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(batch => batch.TenantId == tenantId && batch.CreatedAt >= from && batch.CreatedAt <= to)
            .OrderByDescending(batch => batch.CreatedAt)
            .Take(SourceQueryTake)
            .Select(batch => new
            {
                batch.Id,
                batch.SourceSystem,
                batch.NormalizedSourceSystem,
                batch.Status,
                batch.CreatedAt,
                batch.ValidatedAt,
                batch.StagedAt,
                batch.Description
            })
            .ToListAsync(cancellationToken);

        foreach (var batch in importBatches)
        {
            AddImportLifecycle(events, batch.Id, batch.SourceSystem, batch.NormalizedSourceSystem, "ImportBatchCreated",
                $"Import batch created ({batch.Status})", batch.Description ?? batch.SourceSystem, batch.CreatedAt, batch.Status);
            if (batch.ValidatedAt is not null && batch.ValidatedAt >= from && batch.ValidatedAt <= to)
            {
                AddImportLifecycle(events, batch.Id, batch.SourceSystem, batch.NormalizedSourceSystem, "ImportBatchValidated",
                    "Import batch validated", batch.Description ?? batch.SourceSystem, batch.ValidatedAt.Value, batch.Status);
            }

            if (batch.StagedAt is not null && batch.StagedAt >= from && batch.StagedAt <= to)
            {
                AddImportLifecycle(events, batch.Id, batch.SourceSystem, batch.NormalizedSourceSystem, "ImportBatchStaged",
                    "Import batch staged", batch.Description ?? batch.SourceSystem, batch.StagedAt.Value, batch.Status);
            }
        }

        var toolRuns = await dbContext.ToolRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && run.CreatedAt >= from && run.CreatedAt <= to)
            .OrderByDescending(run => run.CreatedAt)
            .Take(SourceQueryTake)
            .Select(run => new { run.Id, run.Status, run.CreatedAt, run.AiTraceRecordId, run.IsDryRun })
            .ToListAsync(cancellationToken);

        foreach (var run in toolRuns)
        {
            events.Add(new DigitalThreadEventResponse(
                $"tool-run:{run.Id}",
                run.CreatedAt,
                "tool-runtime",
                "Tool Runtime",
                "ToolRun",
                run.IsDryRun ? "Tool dry-run" : "Tool run",
                $"Status: {run.Status}",
                null,
                MapTrustFromStatus(run.Status),
                MapSyncFromStatus(run.Status),
                MapSeverityFromStatus(run.Status),
                run.AiTraceRecordId,
                null));
        }

        var agentRuns = await dbContext.AgentRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && run.StartedAt >= from && run.StartedAt <= to)
            .OrderByDescending(run => run.StartedAt)
            .Take(SourceQueryTake)
            .Select(run => new
            {
                run.Id,
                run.Status,
                run.StartedAt,
                run.AiTraceRecordId,
                run.RecommendationArtifactId,
                run.IsPreview
            })
            .ToListAsync(cancellationToken);

        foreach (var run in agentRuns)
        {
            events.Add(new DigitalThreadEventResponse(
                $"agent-run:{run.Id}",
                run.StartedAt,
                "agent-runtime",
                "Agent Runtime",
                "AgentRun",
                run.IsPreview ? "Agent preview run" : "Agent run",
                $"Status: {run.Status}",
                run.RecommendationArtifactId,
                MapTrustFromStatus(run.Status),
                MapSyncFromStatus(run.Status),
                MapSeverityFromStatus(run.Status),
                run.AiTraceRecordId,
                run.RecommendationArtifactId));
        }

        var workflowRuns = await dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && run.StartedAt >= from && run.StartedAt <= to)
            .OrderByDescending(run => run.StartedAt)
            .Take(SourceQueryTake)
            .Select(run => new { run.Id, run.Status, run.StartedAt, run.AiTraceRecordId, run.IsPreview })
            .ToListAsync(cancellationToken);

        foreach (var run in workflowRuns)
        {
            events.Add(new DigitalThreadEventResponse(
                $"workflow-run:{run.Id}",
                run.StartedAt,
                "workflow-runtime",
                "Workflow Runtime",
                "WorkflowRun",
                run.IsPreview ? "Workflow preview run" : "Workflow run",
                $"Status: {run.Status}",
                null,
                MapTrustFromStatus(run.Status),
                MapSyncFromStatus(run.Status),
                MapSeverityFromStatus(run.Status),
                run.AiTraceRecordId,
                null));
        }

        var dqIssues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(issue => issue.TenantId == tenantId && issue.CreatedAt >= from && issue.CreatedAt <= to)
            .OrderByDescending(issue => issue.CreatedAt)
            .Take(SourceQueryTake)
            .Select(issue => new
            {
                issue.Id,
                issue.Title,
                issue.IssueCode,
                issue.Severity,
                issue.Status,
                issue.CreatedAt,
                issue.ResultingTrustState,
                issue.ImportBatchId
            })
            .ToListAsync(cancellationToken);

        foreach (var issue in dqIssues)
        {
            events.Add(new DigitalThreadEventResponse(
                $"dq:{issue.Id}",
                issue.CreatedAt,
                "data-quality",
                "Data Quality",
                "DataQualityIssue",
                issue.Title,
                $"{issue.IssueCode} · {issue.Status}",
                issue.ImportBatchId,
                issue.ResultingTrustState.ToString(),
                issue.Severity is DataQualitySeverity.High or DataQualitySeverity.Critical ? "Error" : "Warning",
                issue.Severity.ToString().ToLowerInvariant(),
                null,
                null));
        }

        var recommendations = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.NormalizedArtifactType == RecommendationType
                && item.CreatedAt >= from
                && item.CreatedAt <= to)
            .OrderByDescending(item => item.CreatedAt)
            .Take(SourceQueryTake)
            .Select(item => new { item.Id, item.Name, item.Description, item.CreatedAt })
            .ToListAsync(cancellationToken);

        foreach (var recommendation in recommendations)
        {
            events.Add(new DigitalThreadEventResponse(
                $"recommendation:{recommendation.Id}",
                recommendation.CreatedAt,
                "recommendations",
                "Recommendations",
                "RecommendationCreated",
                recommendation.Name,
                recommendation.Description ?? "Recommendation artifact created",
                recommendation.Id,
                "Unverified",
                "OK",
                "info",
                null,
                recommendation.Id));
        }

        var auditRecords = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.CreatedAt >= from
                && item.CreatedAt <= to
                && (item.Result == AuditResult.Denied
                    || item.Result == AuditResult.Failed
                    || item.Result == AuditResult.SecurityEvent))
            .OrderByDescending(item => item.CreatedAt)
            .Take(SourceQueryTake)
            .Select(item => new { item.Id, item.Action, item.SafeSummary, item.CreatedAt, item.Result, item.SourceObjectId })
            .ToListAsync(cancellationToken);

        foreach (var audit in auditRecords)
        {
            Guid? artifactId = Guid.TryParse(audit.SourceObjectId, out var parsed) ? parsed : null;
            events.Add(new DigitalThreadEventResponse(
                $"audit:{audit.Id}",
                audit.CreatedAt,
                "governance",
                "Governance",
                "AuditSignal",
                audit.Action,
                audit.SafeSummary,
                artifactId,
                "Unverified",
                audit.Result == AuditResult.Failed ? "Error" : "Warning",
                audit.Result == AuditResult.Failed ? "high" : "medium",
                null,
                null));
        }

        var securityEvents = await dbContext.SecurityEvents
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.CreatedAt >= from && item.CreatedAt <= to)
            .OrderByDescending(item => item.CreatedAt)
            .Take(SourceQueryTake)
            .Select(item => new { item.Id, item.EventType, item.Severity, item.SafeSummary, item.CreatedAt, item.SourceAction })
            .ToListAsync(cancellationToken);

        foreach (var security in securityEvents)
        {
            events.Add(new DigitalThreadEventResponse(
                $"security:{security.Id}",
                security.CreatedAt,
                "security",
                "Security",
                "SecurityEvent",
                security.EventType.ToString(),
                security.SafeSummary,
                null,
                "Conflicted",
                security.Severity is SecurityEventSeverity.High or SecurityEventSeverity.Critical ? "Error" : "Warning",
                security.Severity.ToString().ToLowerInvariant(),
                null,
                null));
        }

        if (normalizedSystem is not null)
        {
            events.RemoveAll(item => !item.SourceSystemId.Equals(normalizedSystem, StringComparison.OrdinalIgnoreCase));
        }

        return events;
    }

    private static void AddImportLifecycle(
        List<DigitalThreadEventResponse> events,
        Guid batchId,
        string sourceSystem,
        string normalizedSourceSystem,
        string eventType,
        string title,
        string description,
        DateTimeOffset timestamp,
        ImportBatchStatus status)
    {
        var sync = status == ImportBatchStatus.Failed ? "Error"
            : status is ImportBatchStatus.Rejected ? "Warning"
            : "OK";
        events.Add(new DigitalThreadEventResponse(
            $"{eventType.ToLowerInvariant()}:{batchId}:{timestamp.UtcTicks}",
            timestamp,
            NormalizeSystemId(normalizedSourceSystem),
            sourceSystem,
            eventType,
            title,
            description,
            batchId,
            status == ImportBatchStatus.Promoted ? "Trusted" : "Unverified",
            sync,
            status == ImportBatchStatus.Failed ? "high" : "info",
            null,
            null));
    }

    private static IReadOnlyCollection<DigitalThreadHeatmapBucketResponse> BuildHeatmap(
        IReadOnlyList<DigitalThreadEventResponse> events,
        DateTimeOffset windowStart,
        DateTimeOffset now)
    {
        if (events.Count == 0)
        {
            return [];
        }

        var buckets = new Dictionary<(string SystemId, long BucketTicks), int>();
        foreach (var item in events)
        {
            var aligned = AlignToBucket(item.TimestampUtc);
            if (aligned < windowStart || aligned > now)
            {
                continue;
            }

            var key = (item.SourceSystemId, aligned.UtcTicks);
            buckets[key] = buckets.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return buckets
            .Select(pair => new DigitalThreadHeatmapBucketResponse(
                pair.Key.SystemId,
                new DateTimeOffset(pair.Key.BucketTicks, TimeSpan.Zero),
                pair.Value))
            .OrderBy(item => item.SystemId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BucketStartUtc)
            .ToList();
    }

    private static DateTimeOffset AlignToBucket(DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        var hour = utc.Hour - (utc.Hour % HeatmapBucketHours);
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, hour, 0, 0, TimeSpan.Zero);
    }

    private async Task<ActiveTenantContext> RequireReadAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DigitalThreadPermissions.Read, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DigitalThreadPermissions.Admin, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            throw new TenantAccessDeniedException("Digital thread read permission is required.");
        }

        return context;
    }

    private static int ResolveWindowHours(int? windowHours)
    {
        var value = windowHours ?? DefaultWindowHours;
        if (value < 1 || value > MaxWindowHours)
        {
            throw new RequestValidationException($"windowHours must be between 1 and {MaxWindowHours}.");
        }

        return value;
    }

    private static int ResolveEventLimit(int? limit)
    {
        var value = limit ?? DefaultEventLimit;
        if (value < 1 || value > MaxEventLimit)
        {
            throw new RequestValidationException($"limit must be between 1 and {MaxEventLimit}.");
        }

        return value;
    }

    private static string NormalizeSystemId(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        return string.Join('-', trimmed.Split([' ', '_', '/', '\\', '.'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string InferSystemType(string systemId) => systemId switch
    {
        "tool-runtime" => "Runtime",
        "agent-runtime" => "Runtime",
        "workflow-runtime" => "Runtime",
        "data-quality" => "Quality",
        "recommendations" => "Governance",
        "governance" => "Governance",
        "security" => "Security",
        _ => "System"
    };

    private static string MapSyncFromStatus(string status)
    {
        if (status.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || status.Contains("blocked", StringComparison.OrdinalIgnoreCase)
            || status.Contains("denied", StringComparison.OrdinalIgnoreCase))
        {
            return "Error";
        }

        if (status.Contains("safe", StringComparison.OrdinalIgnoreCase)
            || status.Contains("pending", StringComparison.OrdinalIgnoreCase)
            || status.Contains("running", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "OK";
    }

    private static string MapTrustFromStatus(string status)
        => MapSyncFromStatus(status) switch
        {
            "Error" => "Conflicted",
            "Warning" => "Unverified",
            _ => "Trusted"
        };

    private static string? MapSeverityFromStatus(string status)
        => MapSyncFromStatus(status) switch
        {
            "Error" => "high",
            "Warning" => "medium",
            _ => "info"
        };

    private static string PreferWorseStatus(string left, string right)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Healthy"] = 0,
            ["Warning"] = 1,
            ["Down"] = 2
        };
        return rank.GetValueOrDefault(left) >= rank.GetValueOrDefault(right) ? left : right;
    }

    private static string PreferWorseSync(string left, string right)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["OK"] = 0,
            ["Warning"] = 1,
            ["Error"] = 2
        };
        return rank.GetValueOrDefault(left) >= rank.GetValueOrDefault(right) ? left : right;
    }

    private static DateTimeOffset? MaxNullable(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return left >= right ? left : right;
    }

    private static (DateTimeOffset WindowStart, DateTimeOffset WindowEnd, int ResolvedHours) ResolveWindow(
        int? windowHours,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var windowEnd = to ?? DateTimeOffset.UtcNow;
        if (from is not null)
        {
            if (from > windowEnd)
            {
                throw new RequestValidationException("'from' must be earlier than or equal to 'to'.");
            }

            var hours = (int)Math.Ceiling((windowEnd - from.Value).TotalHours);
            hours = Math.Clamp(hours < 1 ? 1 : hours, 1, MaxWindowHours);
            return (from.Value, windowEnd, hours);
        }

        var resolvedHours = ResolveWindowHours(windowHours);
        return (windowEnd.AddHours(-resolvedHours), windowEnd, resolvedHours);
    }

    private static IReadOnlyCollection<DigitalThreadBranchResponse> BuildBranches(
        IReadOnlyList<DigitalThreadEventResponse> events,
        IReadOnlyList<DigitalThreadSystemResponse> systems,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        if (events.Count == 0 && systems.Count == 0)
        {
            return [];
        }

        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Find(string id)
        {
            if (!parent.TryGetValue(id, out var value))
            {
                parent[id] = id;
                return id;
            }

            if (value != id)
            {
                parent[id] = Find(value);
            }

            return parent[id];
        }

        void Union(string left, string right)
        {
            var rootLeft = Find(left);
            var rootRight = Find(right);
            if (!rootLeft.Equals(rootRight, StringComparison.OrdinalIgnoreCase))
            {
                parent[rootRight] = rootLeft;
            }
        }

        foreach (var system in systems)
        {
            Find(system.SystemId);
        }

        foreach (var item in events)
        {
            Find(item.SourceSystemId);
        }

        var bucketGroups = events
            .GroupBy(item => AlignToBucket(item.TimestampUtc).UtcTicks)
            .ToList();
        foreach (var bucket in bucketGroups)
        {
            var ids = bucket
                .Select(item => item.SourceSystemId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var index = 1; index < ids.Count; index += 1)
            {
                Union(ids[0], ids[index]);
            }
        }

        var clusters = parent.Keys
            .GroupBy(Find, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var systemLookup = systems.ToDictionary(
            item => item.SystemId,
            StringComparer.OrdinalIgnoreCase);
        var laneIndex = 0;
        var branches = new List<DigitalThreadBranchResponse>();

        foreach (var cluster in clusters.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var systemIds = cluster
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var clusterEvents = events
                .Where(item => systemIds.Contains(item.SourceSystemId, StringComparer.OrdinalIgnoreCase))
                .OrderBy(item => item.TimestampUtc)
                .ToList();
            if (clusterEvents.Count == 0 && systemIds.Count == 0)
            {
                continue;
            }

            var timeStart = clusterEvents.Count > 0
                ? clusterEvents.Min(item => item.TimestampUtc)
                : windowStart;
            var timeEnd = clusterEvents.Count > 0
                ? clusterEvents.Max(item => item.TimestampUtc)
                : windowEnd;
            var health = ResolveClusterHealth(systemIds, systemLookup, clusterEvents);
            double? trustScore = clusterEvents.Count == 0
                ? null
                : Math.Round(
                    clusterEvents.Average(item => item.TrustState switch
                    {
                        "Trusted" => 1d,
                        "Unverified" => 0.55d,
                        _ => 0.2d
                    }),
                    2);

            var points = BuildProjectionPoints(clusterEvents, systemIds, laneIndex, windowStart, windowEnd);
            branches.Add(new DigitalThreadBranchResponse(
                $"branch-{string.Join('-', systemIds.Take(3))}",
                systemIds,
                timeStart,
                timeEnd,
                clusterEvents.Count,
                health,
                trustScore,
                points));
            laneIndex += 1;
        }

        return branches;
    }

    private static string ResolveClusterHealth(
        IReadOnlyList<string> systemIds,
        IReadOnlyDictionary<string, DigitalThreadSystemResponse> systemLookup,
        IReadOnlyList<DigitalThreadEventResponse> clusterEvents)
    {
        var status = "Healthy";
        foreach (var systemId in systemIds)
        {
            if (systemLookup.TryGetValue(systemId, out var system))
            {
                status = PreferWorseStatus(status, system.ConnectionStatus);
            }
        }

        if (clusterEvents.Any(item => item.SyncStatus.Equals("Error", StringComparison.OrdinalIgnoreCase)))
        {
            status = PreferWorseStatus(status, "Down");
        }
        else if (clusterEvents.Any(item => item.SyncStatus.Equals("Warning", StringComparison.OrdinalIgnoreCase)))
        {
            status = PreferWorseStatus(status, "Warning");
        }

        return status;
    }

    private static IReadOnlyCollection<DigitalThreadProjectionPointResponse> BuildProjectionPoints(
        IReadOnlyList<DigitalThreadEventResponse> clusterEvents,
        IReadOnlyList<string> systemIds,
        int laneIndex,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var spanTicks = Math.Max(1d, (windowEnd - windowStart).TotalSeconds);
        var baseY = 80d + (laneIndex % 8) * 40d;
        if (clusterEvents.Count == 0)
        {
            return systemIds
                .Select((_, index) => new DigitalThreadProjectionPointResponse(
                    80d + index * 60d,
                    baseY))
                .ToList();
        }

        var sampled = clusterEvents
            .Where((_, index) => clusterEvents.Count <= 24 || index % Math.Max(1, clusterEvents.Count / 24) == 0)
            .Take(32)
            .ToList();

        return sampled
            .Select((item, index) =>
            {
                var progress = (item.TimestampUtc - windowStart).TotalSeconds / spanTicks;
                var x = Math.Clamp(progress, 0d, 1d) * (CanvasWidth - 80d) + 40d;
                var systemLane = 0;
                for (var lane = 0; lane < systemIds.Count; lane += 1)
                {
                    if (systemIds[lane].Equals(item.SourceSystemId, StringComparison.OrdinalIgnoreCase))
                    {
                        systemLane = lane;
                        break;
                    }
                }
                var y = baseY + (systemLane * 12d) + Math.Sin(index * 0.7d) * 6d;
                return new DigitalThreadProjectionPointResponse(
                    Math.Round(x, 1),
                    Math.Round(Math.Clamp(y, 20d, CanvasHeight - 20d), 1));
            })
            .ToList();
    }

    private static (double X, double Y) SystemLayoutPoint(int index, int total)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, total))));
        var row = index / columns;
        var col = index % columns;
        var x = 60d + col * ((CanvasWidth - 120d) / Math.Max(1, columns - 1 == 0 ? 1 : columns - 1));
        var y = 60d + row * 48d;
        return (Math.Round(x, 1), Math.Round(y, 1));
    }

    private async Task<DigitalThreadEventResponse?> ResolveEventByIdAsync(
        Guid tenantId,
        string eventId,
        CancellationToken cancellationToken)
    {
        var separator = eventId.IndexOf(':');
        if (separator <= 0)
        {
            return null;
        }

        var prefix = eventId[..separator].ToLowerInvariant();
        var remainder = eventId[(separator + 1)..];

        if (prefix is "tool-run" && Guid.TryParse(remainder, out var toolRunId))
        {
            var run = await dbContext.ToolRuns
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == toolRunId)
                .Select(item => new { item.Id, item.Status, item.CreatedAt, item.AiTraceRecordId, item.IsDryRun })
                .FirstOrDefaultAsync(cancellationToken);
            if (run is null)
            {
                return null;
            }

            return new DigitalThreadEventResponse(
                $"tool-run:{run.Id}",
                run.CreatedAt,
                "tool-runtime",
                "Tool Runtime",
                "ToolRun",
                run.IsDryRun ? "Tool dry-run" : "Tool run",
                $"Status: {run.Status}",
                null,
                MapTrustFromStatus(run.Status),
                MapSyncFromStatus(run.Status),
                MapSeverityFromStatus(run.Status),
                run.AiTraceRecordId,
                null);
        }

        if (prefix is "agent-run" && Guid.TryParse(remainder, out var agentRunId))
        {
            var run = await dbContext.AgentRuns
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == agentRunId)
                .Select(item => new
                {
                    item.Id,
                    item.Status,
                    item.StartedAt,
                    item.AiTraceRecordId,
                    item.RecommendationArtifactId,
                    item.IsPreview
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (run is null)
            {
                return null;
            }

            return new DigitalThreadEventResponse(
                $"agent-run:{run.Id}",
                run.StartedAt,
                "agent-runtime",
                "Agent Runtime",
                "AgentRun",
                run.IsPreview ? "Agent preview run" : "Agent run",
                $"Status: {run.Status}",
                run.RecommendationArtifactId,
                MapTrustFromStatus(run.Status),
                MapSyncFromStatus(run.Status),
                MapSeverityFromStatus(run.Status),
                run.AiTraceRecordId,
                run.RecommendationArtifactId);
        }

        if (prefix is "workflow-run" && Guid.TryParse(remainder, out var workflowRunId))
        {
            var run = await dbContext.WorkflowRuns
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == workflowRunId)
                .Select(item => new { item.Id, item.Status, item.StartedAt, item.AiTraceRecordId, item.IsPreview })
                .FirstOrDefaultAsync(cancellationToken);
            if (run is null)
            {
                return null;
            }

            return new DigitalThreadEventResponse(
                $"workflow-run:{run.Id}",
                run.StartedAt,
                "workflow-runtime",
                "Workflow Runtime",
                "WorkflowRun",
                run.IsPreview ? "Workflow preview run" : "Workflow run",
                $"Status: {run.Status}",
                null,
                MapTrustFromStatus(run.Status),
                MapSyncFromStatus(run.Status),
                MapSeverityFromStatus(run.Status),
                run.AiTraceRecordId,
                null);
        }

        if (prefix is "dq" && Guid.TryParse(remainder, out var dqId))
        {
            var issue = await dbContext.DataQualityIssues
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == dqId)
                .Select(item => new
                {
                    item.Id,
                    item.Title,
                    item.IssueCode,
                    item.Severity,
                    item.Status,
                    item.CreatedAt,
                    item.ResultingTrustState,
                    item.ImportBatchId
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (issue is null)
            {
                return null;
            }

            return new DigitalThreadEventResponse(
                $"dq:{issue.Id}",
                issue.CreatedAt,
                "data-quality",
                "Data Quality",
                "DataQualityIssue",
                issue.Title,
                $"{issue.IssueCode} · {issue.Status}",
                issue.ImportBatchId,
                issue.ResultingTrustState.ToString(),
                issue.Severity is DataQualitySeverity.High or DataQualitySeverity.Critical ? "Error" : "Warning",
                issue.Severity.ToString().ToLowerInvariant(),
                null,
                null);
        }

        if (prefix is "recommendation" && Guid.TryParse(remainder, out var recommendationId))
        {
            var recommendation = await dbContext.Artifacts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.Id == recommendationId
                    && item.NormalizedArtifactType == RecommendationType)
                .Select(item => new { item.Id, item.Name, item.Description, item.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);
            if (recommendation is null)
            {
                return null;
            }

            return new DigitalThreadEventResponse(
                $"recommendation:{recommendation.Id}",
                recommendation.CreatedAt,
                "recommendations",
                "Recommendations",
                "RecommendationCreated",
                recommendation.Name,
                recommendation.Description ?? "Recommendation artifact created",
                recommendation.Id,
                "Unverified",
                "OK",
                "info",
                null,
                recommendation.Id);
        }

        if (prefix is "audit" && Guid.TryParse(remainder, out var auditId))
        {
            var audit = await dbContext.AuditRecords
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == auditId)
                .Select(item => new { item.Id, item.Action, item.SafeSummary, item.CreatedAt, item.Result, item.SourceObjectId })
                .FirstOrDefaultAsync(cancellationToken);
            if (audit is null)
            {
                return null;
            }

            Guid? artifactId = Guid.TryParse(audit.SourceObjectId, out var parsed) ? parsed : null;
            return new DigitalThreadEventResponse(
                $"audit:{audit.Id}",
                audit.CreatedAt,
                "governance",
                "Governance",
                "AuditSignal",
                audit.Action,
                audit.SafeSummary,
                artifactId,
                "Unverified",
                audit.Result == AuditResult.Failed ? "Error" : "Warning",
                audit.Result == AuditResult.Failed ? "high" : "medium",
                null,
                null);
        }

        if (prefix is "security" && Guid.TryParse(remainder, out var securityId))
        {
            var security = await dbContext.SecurityEvents
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == securityId)
                .Select(item => new { item.Id, item.EventType, item.Severity, item.SafeSummary, item.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);
            if (security is null)
            {
                return null;
            }

            return new DigitalThreadEventResponse(
                $"security:{security.Id}",
                security.CreatedAt,
                "security",
                "Security",
                "SecurityEvent",
                security.EventType.ToString(),
                security.SafeSummary,
                null,
                "Conflicted",
                security.Severity is SecurityEventSeverity.High or SecurityEventSeverity.Critical ? "Error" : "Warning",
                security.Severity.ToString().ToLowerInvariant(),
                null,
                null);
        }

        // Import lifecycle ids: eventType:batchId:ticks
        var parts = remainder.Split(':', 2);
        if (parts.Length == 2
            && Guid.TryParse(parts[0], out var batchId)
            && long.TryParse(parts[1], out var ticks))
        {
            var batch = await dbContext.ImportBatches
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == batchId)
                .Select(item => new
                {
                    item.Id,
                    item.SourceSystem,
                    item.NormalizedSourceSystem,
                    item.Status,
                    item.Description
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (batch is null)
            {
                return null;
            }

            var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            var eventType = prefix switch
            {
                "importbatchcreated" => "ImportBatchCreated",
                "importbatchvalidated" => "ImportBatchValidated",
                "importbatchstaged" => "ImportBatchStaged",
                _ => null
            };
            if (eventType is null)
            {
                return null;
            }

            var sync = batch.Status == ImportBatchStatus.Failed ? "Error"
                : batch.Status is ImportBatchStatus.Rejected ? "Warning"
                : "OK";
            return new DigitalThreadEventResponse(
                $"{eventType.ToLowerInvariant()}:{batch.Id}:{ticks}",
                timestamp,
                NormalizeSystemId(batch.NormalizedSourceSystem),
                batch.SourceSystem,
                eventType,
                eventType == "ImportBatchCreated"
                    ? $"Import batch created ({batch.Status})"
                    : eventType == "ImportBatchValidated"
                        ? "Import batch validated"
                        : "Import batch staged",
                batch.Description ?? batch.SourceSystem,
                batch.Id,
                batch.Status == ImportBatchStatus.Promoted ? "Trusted" : "Unverified",
                sync,
                batch.Status == ImportBatchStatus.Failed ? "high" : "info",
                null,
                null);
        }

        return null;
    }
}
