using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AiTrace;

public interface IAiTraceRecorder
{
    Task<Guid> CreateFromRetrievalRunAsync(Guid retrievalRunId, Guid? auditRecordId, CancellationToken cancellationToken);

    Task<Guid> CreateFromChatTurnAsync(Guid chatTurnId, Guid? auditRecordId, CancellationToken cancellationToken);
}

public sealed class AiTraceRecorder(EnterpriseThreadDbContext dbContext) : IAiTraceRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> CreateFromRetrievalRunAsync(Guid retrievalRunId, Guid? auditRecordId, CancellationToken cancellationToken)
    {
        var run = await dbContext.RetrievalRuns
            .Include(item => item.QueryIntentVersion)
            .Include(item => item.RetrievalStrategyVersion)
            .Include(item => item.ContextPackages)
            .SingleOrDefaultAsync(item => item.Id == retrievalRunId, cancellationToken)
            ?? throw new InvalidOperationException("Retrieval run was not found for AI Trace creation.");

        var package = run.ContextPackages.OrderByDescending(item => item.CreatedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Retrieval run has no context package for AI Trace creation.");

        var filteredItems = DeserializeContextItems(package.FilteredContextJson);
        var deniedSummaries = DeserializeDeniedSummaries(package.DeniedSummariesJson);
        var trustFilteredCount = Math.Max(0, run.RetrievedCount - run.FilteredCount);

        var trace = new AiTraceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = run.TenantId,
            RetrievalRunId = run.Id,
            ContextPackageId = package.Id,
            AuditRecordId = auditRecordId,
            TraceKind = AiTraceKind.GovernedQuery,
            IntentKey = run.QueryIntentVersion!.IntentKey,
            StrategyKey = run.RetrievalStrategyVersion!.StrategyKey,
            QueryText = run.QueryText,
            Status = run.Status,
            SafeSummary = $"AI Trace for governed query '{run.QueryIntentVersion.IntentKey}' with {package.AllowedCount} allowed and {package.DeniedCount} denied context items.",
            SourcesSummaryJson = Serialize(BuildSourcesSummary(filteredItems)),
            FilteredSummariesJson = Serialize(filteredItems.Select(ToFilteredSummary).ToList()),
            DeniedSafeSummariesJson = package.DeniedSummariesJson,
            SensitiveDeniedReferencesJson = package.SensitiveDeniedReferencesJson,
            ConfidenceImpactJson = Serialize(new AiTraceConfidenceImpactResponse(
                run.RetrievedCount,
                run.FilteredCount,
                run.DeniedCount,
                trustFilteredCount,
                package.PolicyKey,
                "Trust and policy filtering applied before LLM context assembly.")),
            RequestedByUserId = run.RequestedByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        trace.ArtifactLinks.AddRange(BuildArtifactLinks(trace.TenantId, trace.Id, run, package));
        dbContext.AiTraceRecords.Add(trace);
        await dbContext.SaveChangesAsync(cancellationToken);
        return trace.Id;
    }

    public async Task<Guid> CreateFromChatTurnAsync(Guid chatTurnId, Guid? auditRecordId, CancellationToken cancellationToken)
    {
        var turn = await dbContext.GovernedChatTurns
            .SingleOrDefaultAsync(item => item.Id == chatTurnId, cancellationToken)
            ?? throw new InvalidOperationException("Governed chat turn was not found for AI Trace creation.");

        var run = await dbContext.RetrievalRuns
            .Include(item => item.QueryIntentVersion)
            .Include(item => item.RetrievalStrategyVersion)
            .SingleOrDefaultAsync(item => item.Id == turn.RetrievalRunId, cancellationToken)
            ?? throw new InvalidOperationException("Retrieval run was not found for governed chat AI Trace creation.");

        var package = await dbContext.ContextPackages
            .SingleOrDefaultAsync(item => item.Id == turn.ContextPackageId, cancellationToken)
            ?? throw new InvalidOperationException("Context package was not found for governed chat AI Trace creation.");

        var promptVersion = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == turn.PromptTemplateVersionId, cancellationToken);
        var outputSchemaVersion = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == turn.OutputSchemaVersionId, cancellationToken);

        var filteredItems = DeserializeContextItems(package.FilteredContextJson);
        var trustFilteredCount = Math.Max(0, run.RetrievedCount - run.FilteredCount);
        var confidence = JsonSerializer.Deserialize<GovernedChatConfidenceResponse>(turn.ConfidenceJson, JsonOptions);

        var trace = new AiTraceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = turn.TenantId,
            RetrievalRunId = run.Id,
            ContextPackageId = package.Id,
            AuditRecordId = auditRecordId,
            GovernedChatTurnId = turn.Id,
            TraceKind = AiTraceKind.GovernedChat,
            IntentKey = run.QueryIntentVersion!.IntentKey,
            StrategyKey = run.RetrievalStrategyVersion!.StrategyKey,
            QueryText = turn.UserMessage,
            Status = run.Status,
            SafeSummary = $"AI Trace for governed chat turn with {package.AllowedCount} allowed and {package.DeniedCount} denied context items.",
            SourcesSummaryJson = Serialize(BuildSourcesSummary(filteredItems)),
            FilteredSummariesJson = Serialize(filteredItems.Select(ToFilteredSummary).ToList()),
            DeniedSafeSummariesJson = package.DeniedSummariesJson,
            SensitiveDeniedReferencesJson = package.SensitiveDeniedReferencesJson,
            ConfidenceImpactJson = Serialize(new AiTraceConfidenceImpactResponse(
                run.RetrievedCount,
                run.FilteredCount,
                run.DeniedCount,
                trustFilteredCount,
                package.PolicyKey,
                confidence?.Notes ?? "Governed chat confidence derived from retrieval and policy filtering.")),
            PromptTemplateVersionLabel = promptVersion?.VersionLabel,
            OutputSchemaVersionLabel = outputSchemaVersion?.VersionLabel,
            GeneratedOutputJson = turn.GeneratedOutputJson,
            RequestedByUserId = run.RequestedByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        trace.ArtifactLinks.AddRange(BuildChatArtifactLinks(trace.TenantId, trace.Id, turn, run, package));
        dbContext.AiTraceRecords.Add(trace);
        await dbContext.SaveChangesAsync(cancellationToken);
        return trace.Id;
    }

    private static List<AiTraceArtifactLink> BuildChatArtifactLinks(
        Guid tenantId,
        Guid traceId,
        GovernedChatTurn turn,
        RetrievalRun run,
        ContextPackage package)
    {
        var links = BuildArtifactLinks(tenantId, traceId, run, package);
        links.Add(CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.PromptTemplate, nameof(ArtifactVersion), turn.PromptTemplateVersionId.ToString()));
        links.Add(CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.OutputSchema, nameof(ArtifactVersion), turn.OutputSchemaVersionId.ToString()));
        links.Add(CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.GovernedChatTurn, nameof(GovernedChatTurn), turn.Id.ToString()));

        if (turn.DraftArtifactVersionId is not null)
        {
            links.Add(CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.DraftArtifact, nameof(ArtifactVersion), turn.DraftArtifactVersionId.Value.ToString()));
        }

        return links;
    }

    private static List<AiTraceArtifactLink> BuildArtifactLinks(
        Guid tenantId,
        Guid traceId,
        RetrievalRun run,
        ContextPackage package)
    {
        var links = new List<AiTraceArtifactLink>
        {
            CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.QueryIntent, nameof(QueryIntentVersion), run.QueryIntentVersionId.ToString()),
            CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.RetrievalStrategy, nameof(RetrievalStrategyVersion), run.RetrievalStrategyVersionId.ToString()),
            CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.ContextPackage, nameof(ContextPackage), package.Id.ToString()),
            CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.RetrievalRun, nameof(RetrievalRun), run.Id.ToString())
        };

        if (run.StartGraphNodeId is not null)
        {
            links.Add(CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.GraphNode, "GraphNode", run.StartGraphNodeId.Value.ToString()));
        }

        if (run.DocumentArtifactId is not null)
        {
            links.Add(CreateLink(tenantId, traceId, AiTraceArtifactLinkKind.DocumentArtifact, "DocumentArtifact", run.DocumentArtifactId.Value.ToString()));
        }

        return links;
    }

    private static AiTraceArtifactLink CreateLink(
        Guid tenantId,
        Guid traceId,
        AiTraceArtifactLinkKind linkKind,
        string objectType,
        string objectId)
    {
        return new AiTraceArtifactLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AiTraceRecordId = traceId,
            LinkKind = linkKind,
            ObjectType = objectType,
            ObjectId = objectId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static IReadOnlyCollection<AiTraceSourceSummaryResponse> BuildSourcesSummary(
        IReadOnlyCollection<ContextItemSnapshot> filteredItems)
    {
        return filteredItems
            .GroupBy(item => item.SourceKind, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AiTraceSourceSummaryResponse(
                group.Key,
                group.Count(),
                group.Select(item => item.SafeSummary).Take(20).ToList()))
            .OrderBy(item => item.SourceKind, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TraceContextSummaryResponse ToFilteredSummary(ContextItemSnapshot item)
    {
        return new TraceContextSummaryResponse(item.ContextId, item.ContextType, item.SourceKind, item.SafeSummary);
    }

    private static IReadOnlyCollection<ContextItemSnapshot> DeserializeContextItems(string json)
        => JsonSerializer.Deserialize<IReadOnlyCollection<ContextItemSnapshot>>(json, JsonOptions) ?? [];

    private static IReadOnlyCollection<DeniedSummarySnapshot> DeserializeDeniedSummaries(string json)
        => JsonSerializer.Deserialize<IReadOnlyCollection<DeniedSummarySnapshot>>(json, JsonOptions) ?? [];

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private sealed record ContextItemSnapshot(
        string ContextId,
        string ContextType,
        string ClassificationKey,
        string? AttributeKey,
        string? DocumentId,
        string SourceKind,
        int DisplayOrder,
        string SafeSummary);

    private sealed record DeniedSummarySnapshot(
        string ContextId,
        string ContextType,
        string SafeSummary,
        string Reason);
}
