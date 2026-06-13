using ETOS.Backend.GraphMemory;

namespace ETOS.Backend.GovernedQuery;

public static class GovernedQueryPermissions
{
    public const string Read = "governed_query.read";
    public const string Run = "governed_query.run";
    public const string Admin = "governed_query.admin";
}

public sealed record RunGovernedQueryRequest(
    string IntentKey,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    string? PolicyKey,
    string? QueryText,
    int MaxDepth);

public sealed record QueryIntentVersionResponse(
    Guid Id,
    Guid TenantId,
    string IntentKey,
    string VersionLabel,
    string Name,
    string? Summary,
    QueryIntentKind IntentKind,
    QueryIntentSource Source,
    bool IsEnabled,
    DateTimeOffset CreatedAt);

public sealed record RetrievalStrategyVersionResponse(
    Guid Id,
    Guid TenantId,
    string StrategyKey,
    string VersionLabel,
    string Name,
    string? Summary,
    GraphSpace GraphSpace,
    TrustState RequiredTrustState,
    IReadOnlyCollection<string> RelationshipTypes,
    bool AllowsSemanticFallback,
    bool AllowsVectorFallback,
    QueryIntentSource Source,
    bool IsEnabled,
    DateTimeOffset CreatedAt);

public sealed record ContextItemResponse(
    string ContextId,
    string ContextType,
    string ClassificationKey,
    string? AttributeKey,
    string? DocumentId,
    string SourceKind,
    int DisplayOrder,
    string SafeSummary);

public sealed record ContextAccessDecisionResponse(
    Guid Id,
    Guid TenantId,
    Guid ContextPackageId,
    string ContextId,
    string ContextType,
    ContextDecisionResult Result,
    string SafeSummary,
    string? Reason,
    int DisplayOrder,
    DateTimeOffset CreatedAt);

public sealed record ContextPackageResponse(
    Guid Id,
    Guid TenantId,
    Guid RetrievalRunId,
    string? PolicyKey,
    Guid? PolicyEvaluationId,
    IReadOnlyCollection<ContextItemResponse> RetrievedContext,
    IReadOnlyCollection<ContextItemResponse> FilteredContext,
    IReadOnlyCollection<ContextItemResponse> LlmVisibleContext,
    IReadOnlyCollection<DeniedContextSummaryResponse> DeniedSummaries,
    IReadOnlyCollection<SensitiveDeniedContextReferenceResponse> SensitiveDeniedReferences,
    IReadOnlyCollection<ContextAccessDecisionResponse> AccessDecisions,
    int AllowedCount,
    int DeniedCount,
    string SafeSummary,
    DateTimeOffset CreatedAt);

public sealed record RetrievalRunResponse(
    Guid Id,
    Guid TenantId,
    QueryIntentVersionResponse QueryIntent,
    RetrievalStrategyVersionResponse RetrievalStrategy,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    string QueryText,
    string Status,
    int RetrievedCount,
    int FilteredCount,
    int DeniedCount,
    string SafeSummary,
    Guid RequestedByUserId,
    Guid? AuditRecordId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    ContextPackageResponse? ContextPackage);

public sealed record RetrievalRunSummaryResponse(
    Guid Id,
    Guid TenantId,
    string IntentKey,
    string StrategyKey,
    Guid? StartGraphNodeId,
    Guid? DocumentArtifactId,
    string Status,
    int RetrievedCount,
    int FilteredCount,
    int DeniedCount,
    string SafeSummary,
    Guid RequestedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record DeniedContextSummaryResponse(
    string ContextId,
    string ContextType,
    string SafeSummary,
    string Reason);

public sealed record SensitiveDeniedContextReferenceResponse(
    string ContextId,
    string ContextType,
    string? DocumentId,
    string ClassificationKey,
    string? AttributeKey,
    string Reason);
