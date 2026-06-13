using System.Text.Json.Serialization;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.GovernedQuery;

public sealed class QueryIntentVersion : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string IntentKey { get; set; }
    public required string NormalizedIntentKey { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public required string Name { get; set; }
    public string? Summary { get; set; }
    public QueryIntentKind IntentKind { get; set; }
    public QueryIntentSource Source { get; set; } = QueryIntentSource.PlatformFixed;
    public bool IsEnabled { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RetrievalStrategyVersion : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string StrategyKey { get; set; }
    public required string NormalizedStrategyKey { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public required string Name { get; set; }
    public string? Summary { get; set; }
    public GraphSpace GraphSpace { get; set; } = GraphSpace.Trusted;
    public TrustState RequiredTrustState { get; set; } = TrustState.Trusted;
    public required string RelationshipTypesJson { get; set; }
    public bool AllowsSemanticFallback { get; set; }
    public bool AllowsVectorFallback { get; set; }
    public QueryIntentSource Source { get; set; } = QueryIntentSource.PlatformFixed;
    public bool IsEnabled { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RetrievalRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid QueryIntentVersionId { get; set; }
    public QueryIntentVersion? QueryIntentVersion { get; set; }
    public Guid RetrievalStrategyVersionId { get; set; }
    public RetrievalStrategyVersion? RetrievalStrategyVersion { get; set; }
    public Guid? StartGraphNodeId { get; set; }
    public Guid? DocumentArtifactId { get; set; }
    public required string QueryText { get; set; }
    public required string Status { get; set; }
    public int RetrievedCount { get; set; }
    public int FilteredCount { get; set; }
    public int DeniedCount { get; set; }
    public required string SafeSummary { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<ContextPackage> ContextPackages { get; set; } = [];
}

public sealed class ContextPackage : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RetrievalRunId { get; set; }
    public RetrievalRun? RetrievalRun { get; set; }
    public string? PolicyKey { get; set; }
    public Guid? PolicyEvaluationId { get; set; }
    public required string RetrievedContextJson { get; set; }
    public required string FilteredContextJson { get; set; }
    public required string DeniedSummariesJson { get; set; }
    public required string SensitiveDeniedReferencesJson { get; set; }
    public required string LlmVisibleContextJson { get; set; }
    public int AllowedCount { get; set; }
    public int DeniedCount { get; set; }
    public required string SafeSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ContextAccessDecision> AccessDecisions { get; set; } = [];
}

public sealed class ContextAccessDecision : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContextPackageId { get; set; }
    public ContextPackage? ContextPackage { get; set; }
    public required string ContextId { get; set; }
    public required string ContextType { get; set; }
    public ContextDecisionResult Result { get; set; }
    public required string SafeSummary { get; set; }
    public string? Reason { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryIntentKind
{
    Object360Context = 0,
    BomImpactContext = 1,
    DocumentEvidenceContext = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryIntentSource
{
    PlatformFixed = 0,
    TenantPlaceholder = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContextDecisionResult
{
    Allowed = 0,
    Filtered = 1,
    Denied = 2
}
