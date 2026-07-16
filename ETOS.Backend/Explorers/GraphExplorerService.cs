using System.Text.Json;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.GovernanceAnalytics;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Explorers;

public interface IGraphExplorerService
{
    Task<GraphExplorerNodeListResponse> ListNodesAsync(
        GraphSpace? graphSpace,
        TrustState? trustState,
        string? objectType,
        string? search,
        int? limit,
        string? policyKey,
        CancellationToken cancellationToken);

    Task<GraphExplorerNodeDetailResponse> GetNodeAsync(
        Guid nodeId,
        string? policyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GraphExplorerRelationshipResponse>> ListRelationshipsAsync(
        Guid nodeId,
        string? direction,
        string? policyKey,
        CancellationToken cancellationToken);

    Task<GraphExplorerSubgraphResponse> GetSubgraphAsync(
        Guid nodeId,
        int? depth,
        string? relationshipTypes,
        string? direction,
        GraphSpace? graphSpace,
        TrustState? trustState,
        int? limit,
        string? policyKey,
        CancellationToken cancellationToken);

    Task<GraphExplorerPatternQueryResponse> QueryPatternAsync(
        GraphExplorerPatternQueryRequest request,
        CancellationToken cancellationToken);
}

public sealed class GraphExplorerService(
    IGraphMemoryService graphMemoryService,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    ExplorerPolicyFilter policyFilter) : IGraphExplorerService
{
    private const int DefaultLimit = 25;
    private const int MaxListLimit = 100;
    private const int DefaultSubgraphLimit = 100;
    private const int MaxSubgraphLimit = 250;
    private const int DefaultDepth = 2;
    private const int MaxDepth = 5;
    private const int MaxPatternSeeds = 20;

    public async Task<GraphExplorerNodeListResponse> ListNodesAsync(
        GraphSpace? graphSpace,
        TrustState? trustState,
        string? objectType,
        string? search,
        int? limit,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.list",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var resolvedLimit = NormalizeListLimit(limit);
        var resolvedSpace = graphSpace ?? GraphSpace.Trusted;
        var minimumTrust = trustState ?? (resolvedSpace == GraphSpace.Staging ? TrustState.Provisional : TrustState.Trusted);
        var graph = await graphMemoryService.ListGraphAsync(
            context.TenantId,
            resolvedSpace,
            null,
            null,
            null,
            cancellationToken);

        var matched = graph.Nodes
            .Where(node => ExplorerPolicyFilter.MeetsTrustFilter(node, minimumTrust))
            .Where(node => string.IsNullOrWhiteSpace(objectType)
                || string.Equals(node.ObjectType, objectType.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(node => MatchesSearch(node, search))
            .ToList();

        var truncated = matched.Count > resolvedLimit;
        var page = matched.Take(resolvedLimit).ToList();
        var responses = new List<GraphExplorerNodeSummaryResponse>(page.Count);
        foreach (var node in page)
        {
            responses.Add(await MapNodeSummaryAsync(node, policyKey, cancellationToken));
        }

        return new GraphExplorerNodeListResponse(responses, truncated, resolvedLimit, matched.Count);
    }

    public async Task<GraphExplorerNodeDetailResponse> GetNodeAsync(
        Guid nodeId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.get",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var node = await graphMemoryService.GetNodeAsync(context.TenantId, nodeId, cancellationToken)
            ?? throw new RequestValidationException("Graph node was not found.");

        var filtered = await policyFilter.FilterNodeAsync(node, policyKey, cancellationToken);
        return new GraphExplorerNodeDetailResponse(
            node.NodeId,
            node.ObjectType,
            node.TrustState.ToString(),
            node.GraphSpace.ToString(),
            filtered.SafeSummary,
            node.SourceReference?.SourceBatchId,
            filtered.AllowedAttributes,
            $"/graph/{node.NodeId}",
            $"/chat?startGraphNodeId={node.NodeId}");
    }

    public async Task<IReadOnlyCollection<GraphExplorerRelationshipResponse>> ListRelationshipsAsync(
        Guid nodeId,
        string? direction,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.relationships",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var traversal = await graphMemoryService.TraverseAsync(
            new TraverseGraphRequest(
                context.TenantId,
                nodeId,
                null,
                1,
                null,
                [TrustState.Trusted, TrustState.Provisional]),
            cancellationToken);

        var normalizedDirection = string.IsNullOrWhiteSpace(direction) ? "both" : direction.Trim().ToLowerInvariant();
        var responses = new List<GraphExplorerRelationshipResponse>();

        foreach (var relationship in traversal.Relationships)
        {
            var isOutgoing = relationship.FromNodeId == nodeId;
            var isIncoming = relationship.ToNodeId == nodeId;
            if (normalizedDirection == "out" && !isOutgoing)
            {
                continue;
            }

            if (normalizedDirection == "in" && !isIncoming)
            {
                continue;
            }

            var adjacentNodeId = isOutgoing ? relationship.ToNodeId : relationship.FromNodeId;
            var adjacentNode = traversal.Nodes.SingleOrDefault(node => node.NodeId == adjacentNodeId);
            if (adjacentNode is null)
            {
                continue;
            }

            var filtered = await policyFilter.FilterNodeAsync(adjacentNode, policyKey, cancellationToken);
            responses.Add(new GraphExplorerRelationshipResponse(
                relationship.RelationshipId,
                relationship.RelationshipType,
                isOutgoing ? "out" : "in",
                adjacentNodeId,
                adjacentNode.ObjectType,
                adjacentNode.TrustState.ToString(),
                filtered.SafeSummary));
        }

        return responses;
    }

    public async Task<GraphExplorerSubgraphResponse> GetSubgraphAsync(
        Guid nodeId,
        int? depth,
        string? relationshipTypes,
        string? direction,
        GraphSpace? graphSpace,
        TrustState? trustState,
        int? limit,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.nodes.subgraph",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        var resolvedDepth = NormalizeDepth(depth, DefaultDepth);
        var resolvedLimit = NormalizeSubgraphLimit(limit);
        var relTypes = ParseRelationshipTypes(relationshipTypes);
        var allowedTrust = ResolveAllowedTrustStates(trustState);
        var normalizedDirection = string.IsNullOrWhiteSpace(direction)
            ? "out"
            : direction.Trim().ToLowerInvariant();

        _ = await graphMemoryService.GetNodeAsync(context.TenantId, nodeId, cancellationToken)
            ?? throw new RequestValidationException("Graph node was not found.");

        var traversal = await graphMemoryService.TraverseAsync(
            new TraverseGraphRequest(
                context.TenantId,
                nodeId,
                graphSpace,
                resolvedDepth,
                relTypes,
                allowedTrust),
            cancellationToken);

        var relationships = FilterRelationshipsByDirection(traversal.Relationships, nodeId, normalizedDirection);
        var nodeMap = traversal.Nodes.ToDictionary(node => node.NodeId);
        if (!nodeMap.ContainsKey(traversal.StartNode.NodeId))
        {
            nodeMap[traversal.StartNode.NodeId] = traversal.StartNode;
        }

        var orderedNodeIds = new List<Guid> { nodeId };
        foreach (var relationship in relationships)
        {
            if (!orderedNodeIds.Contains(relationship.FromNodeId))
            {
                orderedNodeIds.Add(relationship.FromNodeId);
            }

            if (!orderedNodeIds.Contains(relationship.ToNodeId))
            {
                orderedNodeIds.Add(relationship.ToNodeId);
            }
        }

        foreach (var candidateId in nodeMap.Keys)
        {
            if (!orderedNodeIds.Contains(candidateId))
            {
                orderedNodeIds.Add(candidateId);
            }
        }

        var truncated = orderedNodeIds.Count > resolvedLimit;
        var keptIds = orderedNodeIds.Take(resolvedLimit).ToHashSet();
        var nodeResponses = new List<GraphExplorerNodeSummaryResponse>();
        foreach (var keptId in keptIds)
        {
            if (!nodeMap.TryGetValue(keptId, out var node))
            {
                continue;
            }

            nodeResponses.Add(await MapNodeSummaryAsync(node, policyKey, cancellationToken));
        }

        var edgeResponses = relationships
            .Where(edge => keptIds.Contains(edge.FromNodeId) && keptIds.Contains(edge.ToNodeId))
            .Select(MapEdge)
            .ToList();

        return new GraphExplorerSubgraphResponse(
            nodeId,
            nodeResponses,
            edgeResponses,
            truncated,
            resolvedDepth,
            resolvedLimit);
    }

    public async Task<GraphExplorerPatternQueryResponse> QueryPatternAsync(
        GraphExplorerPatternQueryRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.graph.pattern_query",
            ExplorerPermissions.GraphExplorer,
            cancellationToken);

        if (request.StartNodeId is null
            && string.IsNullOrWhiteSpace(request.StartObjectType)
            && string.IsNullOrWhiteSpace(request.Search))
        {
            throw new RequestValidationException(
                "Pattern query requires startNodeId, startObjectType, or search.");
        }

        var resolvedDepth = NormalizeDepth(request.MaxDepth, DefaultDepth);
        var resolvedLimit = NormalizeSubgraphLimit(request.Limit);
        var resolvedSpace = ParseGraphSpace(request.GraphSpace) ?? GraphSpace.Trusted;
        var parsedTrust = ParseTrustState(request.TrustState);
        var minimumTrust = parsedTrust
            ?? (resolvedSpace == GraphSpace.Staging ? TrustState.Provisional : TrustState.Trusted);
        var allowedTrust = ResolveAllowedTrustStates(parsedTrust);
        var relTypes = request.RelationshipTypes?
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var seeds = await ResolvePatternSeedsAsync(
            context.TenantId,
            request.StartNodeId,
            request.StartObjectType,
            request.Search,
            resolvedSpace,
            minimumTrust,
            cancellationToken);

        if (seeds.Count == 0)
        {
            return new GraphExplorerPatternQueryResponse([], [], false, resolvedDepth, resolvedLimit, 0);
        }

        var nodeMap = new Dictionary<Guid, BaseNode>();
        var edgeMap = new Dictionary<Guid, BaseRelationship>();

        foreach (var seed in seeds)
        {
            nodeMap[seed.NodeId] = seed;
            var traversal = await graphMemoryService.TraverseAsync(
                new TraverseGraphRequest(
                    context.TenantId,
                    seed.NodeId,
                    resolvedSpace,
                    resolvedDepth,
                    relTypes is { Length: > 0 } ? relTypes : null,
                    allowedTrust),
                cancellationToken);

            foreach (var node in traversal.Nodes)
            {
                nodeMap[node.NodeId] = node;
            }

            nodeMap[traversal.StartNode.NodeId] = traversal.StartNode;

            foreach (var relationship in traversal.Relationships)
            {
                edgeMap[relationship.RelationshipId] = relationship;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.EndObjectType))
        {
            var endType = request.EndObjectType.Trim();
            var endNodeIds = nodeMap.Values
                .Where(node => string.Equals(node.ObjectType, endType, StringComparison.OrdinalIgnoreCase))
                .Select(node => node.NodeId)
                .ToHashSet();
            var seedIds = seeds.Select(seed => seed.NodeId).ToHashSet();

            if (endNodeIds.Count == 0)
            {
                // No end-type match: return seeds only.
                edgeMap.Clear();
                nodeMap = nodeMap
                    .Where(pair => seedIds.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
            else
            {
                var keepNodes = new HashSet<Guid>(seedIds);
                keepNodes.UnionWith(endNodeIds);
                foreach (var edge in edgeMap.Values)
                {
                    if (keepNodes.Contains(edge.FromNodeId) || keepNodes.Contains(edge.ToNodeId))
                    {
                        keepNodes.Add(edge.FromNodeId);
                        keepNodes.Add(edge.ToNodeId);
                    }
                }

                edgeMap = edgeMap
                    .Where(pair => keepNodes.Contains(pair.Value.FromNodeId) && keepNodes.Contains(pair.Value.ToNodeId))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                nodeMap = nodeMap
                    .Where(pair => keepNodes.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
        }

        var orderedNodes = nodeMap.Values
            .OrderBy(node => seeds.Any(seed => seed.NodeId == node.NodeId) ? 0 : 1)
            .ThenBy(node => node.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var truncated = orderedNodes.Count > resolvedLimit;
        var keptNodes = orderedNodes.Take(resolvedLimit).ToList();
        var keptIds = keptNodes.Select(node => node.NodeId).ToHashSet();

        var nodeResponses = new List<GraphExplorerNodeSummaryResponse>(keptNodes.Count);
        foreach (var node in keptNodes)
        {
            nodeResponses.Add(await MapNodeSummaryAsync(node, request.PolicyKey, cancellationToken));
        }

        var edgeResponses = edgeMap.Values
            .Where(edge => keptIds.Contains(edge.FromNodeId) && keptIds.Contains(edge.ToNodeId))
            .Select(MapEdge)
            .ToList();

        return new GraphExplorerPatternQueryResponse(
            nodeResponses,
            edgeResponses,
            truncated,
            resolvedDepth,
            resolvedLimit,
            seeds.Count);
    }

    private async Task<IReadOnlyList<BaseNode>> ResolvePatternSeedsAsync(
        Guid tenantId,
        Guid? startNodeId,
        string? startObjectType,
        string? search,
        GraphSpace graphSpace,
        TrustState minimumTrust,
        CancellationToken cancellationToken)
    {
        if (startNodeId is Guid seedId)
        {
            var seed = await graphMemoryService.GetNodeAsync(tenantId, seedId, cancellationToken)
                ?? throw new RequestValidationException("Pattern start node was not found.");
            return [seed];
        }

        var graph = await graphMemoryService.ListGraphAsync(
            tenantId,
            graphSpace,
            null,
            null,
            null,
            cancellationToken);

        return graph.Nodes
            .Where(node => ExplorerPolicyFilter.MeetsTrustFilter(node, minimumTrust))
            .Where(node => string.IsNullOrWhiteSpace(startObjectType)
                || string.Equals(node.ObjectType, startObjectType.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(node => MatchesSearch(node, search))
            .Take(MaxPatternSeeds)
            .ToList();
    }

    private async Task<GraphExplorerNodeSummaryResponse> MapNodeSummaryAsync(
        BaseNode node,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var filtered = await policyFilter.FilterNodeAsync(node, policyKey, cancellationToken);
        return new GraphExplorerNodeSummaryResponse(
            node.NodeId,
            node.ObjectType,
            node.TrustState.ToString(),
            node.GraphSpace.ToString(),
            filtered.SafeSummary,
            node.SourceReference?.SourceBatchId,
            filtered.AllowedAttributes);
    }

    private static GraphExplorerSubgraphEdgeResponse MapEdge(BaseRelationship relationship)
    {
        var summary = relationship.Attributes.TryGetValue("safeSummary", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : $"{relationship.RelationshipType} relationship.";
        return new GraphExplorerSubgraphEdgeResponse(
            relationship.RelationshipId,
            relationship.RelationshipType,
            relationship.FromNodeId,
            relationship.ToNodeId,
            relationship.TrustState.ToString(),
            summary);
    }

    private static IReadOnlyCollection<BaseRelationship> FilterRelationshipsByDirection(
        IReadOnlyCollection<BaseRelationship> relationships,
        Guid focusNodeId,
        string normalizedDirection)
    {
        // Neo4j GraphMemory traverse is outgoing-path oriented; "out"/"both" return traversal edges.
        if (normalizedDirection is "both" or "out")
        {
            return relationships;
        }

        if (normalizedDirection == "in")
        {
            return relationships
                .Where(relationship => relationship.ToNodeId == focusNodeId)
                .ToList();
        }

        return relationships;
    }

    private static bool MatchesSearch(BaseNode node, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        if (node.NodeId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (node.ObjectType.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var summary = ExplorerPolicyFilter.ResolveSafeSummary(node);
        if (summary.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var attribute in node.Attributes)
        {
            if (!string.IsNullOrWhiteSpace(attribute.Value)
                && attribute.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyCollection<string>? ParseRelationshipTypes(string? relationshipTypes)
    {
        if (string.IsNullOrWhiteSpace(relationshipTypes))
        {
            return null;
        }

        var parsed = relationshipTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return parsed.Length == 0 ? null : parsed;
    }

    private static GraphSpace? ParseGraphSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<GraphSpace>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : throw new RequestValidationException($"Unknown graphSpace '{value}'.");
    }

    private static TrustState? ParseTrustState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<TrustState>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : throw new RequestValidationException($"Unknown trustState '{value}'.");
    }

    private static IReadOnlyCollection<TrustState> ResolveAllowedTrustStates(TrustState? trustState)
    {
        if (trustState is null)
        {
            return [TrustState.Trusted, TrustState.Provisional];
        }

        return trustState.Value switch
        {
            TrustState.Trusted => [TrustState.Trusted],
            TrustState.Provisional => [TrustState.Provisional, TrustState.Trusted],
            TrustState.Unverified => [TrustState.Unverified, TrustState.Provisional, TrustState.Trusted],
            TrustState.Conflicted => [TrustState.Conflicted, TrustState.Trusted, TrustState.Provisional],
            _ => [TrustState.Trusted, TrustState.Provisional]
        };
    }

    private static int NormalizeListLimit(int? limit)
    {
        if (limit is null or <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxListLimit);
    }

    private static int NormalizeSubgraphLimit(int? limit)
    {
        if (limit is null or <= 0)
        {
            return DefaultSubgraphLimit;
        }

        return Math.Min(limit.Value, MaxSubgraphLimit);
    }

    private static int NormalizeDepth(int? depth, int fallback)
    {
        if (depth is null or <= 0)
        {
            return fallback;
        }

        return Math.Clamp(depth.Value, 1, MaxDepth);
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks graph explorer permission.");
        }

        return context;
    }
}

public interface IContextPackageExplorerService
{
    Task<IReadOnlyCollection<ContextPackageExplorerSummaryResponse>> ListPackagesAsync(CancellationToken cancellationToken);
    Task<ContextPackageExplorerDetailResponse> GetPackageAsync(Guid packageId, CancellationToken cancellationToken);
}

public sealed class ContextPackageExplorerService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IGovernedQueryService governedQueryService) : IContextPackageExplorerService
{
    public async Task<IReadOnlyCollection<ContextPackageExplorerSummaryResponse>> ListPackagesAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionsAsync(
            "explorers.context_packages.list",
            [ExplorerPermissions.Read, GovernedQueryPermissions.Read],
            cancellationToken);

        var runs = await governedQueryService.ListRunsAsync(cancellationToken);
        var summaries = new List<ContextPackageExplorerSummaryResponse>();
        foreach (var run in runs.OrderByDescending(item => item.CreatedAt).Take(50))
        {
            var packageId = await dbContext.ContextPackages
                .AsNoTracking()
                .Where(package => package.TenantId == context.TenantId && package.RetrievalRunId == run.Id)
                .Select(package => package.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (packageId == Guid.Empty)
            {
                continue;
            }

            Guid? traceId = null;
            if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Read, cancellationToken))
            {
                traceId = await dbContext.AiTraceRecords
                    .AsNoTracking()
                    .Where(trace => trace.TenantId == context.TenantId && trace.RetrievalRunId == run.Id)
                    .Select(trace => (Guid?)trace.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            summaries.Add(new ContextPackageExplorerSummaryResponse(
                packageId,
                run.Id,
                run.IntentKey,
                run.StrategyKey,
                run.RetrievedCount,
                run.FilteredCount,
                run.DeniedCount,
                run.SafeSummary,
                run.CreatedAt,
                traceId));
        }

        return summaries;
    }

    public async Task<ContextPackageExplorerDetailResponse> GetPackageAsync(Guid packageId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionsAsync(
            "explorers.context_packages.get",
            [ExplorerPermissions.Read, GovernedQueryPermissions.Read],
            cancellationToken);

        var package = await governedQueryService.GetContextPackageAsync(packageId, cancellationToken);
        var run = await dbContext.RetrievalRuns
            .AsNoTracking()
            .Join(
                dbContext.QueryIntentVersions,
                item => item.QueryIntentVersionId,
                intent => intent.Id,
                (item, intent) => new { item, intent })
            .Join(
                dbContext.RetrievalStrategyVersions,
                pair => pair.item.RetrievalStrategyVersionId,
                strategy => strategy.Id,
                (pair, strategy) => new
                {
                    pair.item,
                    pair.intent,
                    strategy
                })
            .SingleOrDefaultAsync(pair => pair.item.Id == package.RetrievalRunId && pair.item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Retrieval run was not found.");

        Guid? traceId = null;
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AiTracePermissions.Read, cancellationToken))
        {
            traceId = await dbContext.AiTraceRecords
                .AsNoTracking()
                .Where(trace => trace.TenantId == context.TenantId && trace.RetrievalRunId == run.item.Id)
                .Select(trace => (Guid?)trace.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new ContextPackageExplorerDetailResponse(
            package.Id,
            package.RetrievalRunId,
            run.intent.IntentKey,
            run.strategy.StrategyKey,
            package.AllowedCount,
            package.DeniedCount,
            package.SafeSummary,
            traceId,
            traceId.HasValue ? $"/ai-traces/{traceId}" : null,
            package.DeniedSummaries.Select(item => item.SafeSummary).Take(5).ToList());
    }

    private async Task<ActiveTenantContext> RequirePermissionsAsync(
        string action,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        foreach (var permissionKey in permissionKeys)
        {
            if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
            {
                await denialRecorder.RecordAsync(
                    context.TenantId,
                    context.UserId,
                    action,
                    "permission_denied",
                    $"The user lacks the {permissionKey} permission.",
                    cancellationToken);
                throw new TenantAccessDeniedException($"User lacks {permissionKey} permission.");
            }
        }

        return context;
    }
}

public interface IDecisionExplorerFoundationService
{
    Task<IReadOnlyCollection<DecisionExplorerItemResponse>> ListDecisionsAsync(
        string? status,
        string? participant,
        string? search,
        string? conflict,
        string? outcomeKey,
        bool? hasOutcome,
        int? minEvidenceCount,
        CancellationToken cancellationToken);
}

public interface IArtifactExplorerService
{
    Task<IReadOnlyCollection<ArtifactExplorerSummaryResponse>> ListArtifactsAsync(
        string? artifactType,
        string? lifecycleState,
        string? search,
        CancellationToken cancellationToken);
}

public sealed class DecisionExplorerFoundationService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IDecisionExplorerFoundationService
{
    private static readonly HashSet<string> DecisionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        DecisionArtifactTypes.Decision.ToUpperInvariant(),
        "DECISION",
        "DECISION-ARTIFACT",
        "DECISIONARTIFACT"
    };

    private const int DecisionExplorerLimit = 500;

    public async Task<IReadOnlyCollection<DecisionExplorerItemResponse>> ListDecisionsAsync(
        string? status,
        string? participant,
        string? search,
        string? conflict,
        string? outcomeKey,
        bool? hasOutcome,
        int? minEvidenceCount,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.decisions.list",
            ExplorerPermissions.Read,
            cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId && DecisionTypes.Contains(artifact.NormalizedArtifactType))
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(DecisionExplorerLimit)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);
        var outcomeDecisionIds = await DecisionExplorerQueryHelper.LoadDecisionIdsWithOutcomeChecksAsync(
            dbContext,
            context.TenantId,
            artifactIds,
            cancellationToken);

        var filter = new DecisionExplorerFilter(
            status,
            participant,
            search,
            conflict,
            outcomeKey,
            hasOutcome,
            minEvidenceCount);

        var responses = new List<DecisionExplorerItemResponse>();
        foreach (var artifact in artifacts)
        {
            if (!versionLookup.TryGetValue(artifact.Id, out var version))
            {
                continue;
            }

            DecisionPayloadParser.DecisionPayloadDocument payload;
            try
            {
                payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            }
            catch (Exception)
            {
                continue;
            }

            var hasOutcomeCheckRun = outcomeDecisionIds.Contains(artifact.Id);
            if (!DecisionExplorerQueryHelper.MatchesFilter(payload, artifact.Name, filter, hasOutcomeCheckRun))
            {
                continue;
            }

            responses.Add(new DecisionExplorerItemResponse(
                artifact.Id,
                artifact.ArtifactType,
                payload.Title ?? artifact.Name,
                payload.Status.ToString(),
                payload.ParticipantUserIds?.Select(id => id.ToString()).ToList() ?? [],
                payload.EvidenceReferences?.Count ?? 0,
                payload.ConflictState.ToString(),
                payload.OutcomeSummary ?? "Outcome not recorded.",
                payload.OutcomeKey ?? string.Empty,
                !string.IsNullOrWhiteSpace(payload.OutcomeKey) || hasOutcomeCheckRun,
                $"/decisions/{artifact.Id}"));
        }

        return responses;
    }

    private static (string Title, string Status, IReadOnlyCollection<string> ParticipantUserIds, int EvidenceCount, string ConflictState, string OutcomeSummary) ParseDecisionPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return ("Decision", "draft", [], 0, "unknown", "Outcome not recorded.");
        }

        try
        {
            var payload = DecisionPayloadParser.Deserialize(payloadJson);
            return (
                payload.Title ?? "Decision",
                payload.Status.ToString(),
                payload.ParticipantUserIds?.Select(id => id.ToString()).ToList() ?? [],
                payload.EvidenceReferences?.Count ?? 0,
                payload.ConflictState.ToString(),
                payload.OutcomeSummary ?? "Outcome not recorded.");
        }
        catch (Exception)
        {
            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                var root = document.RootElement;
                return (
                    root.TryGetProperty("title", out var title) ? title.GetString() ?? "Decision" : "Decision",
                    root.TryGetProperty("status", out var status) ? status.GetString() ?? "draft" : "draft",
                    root.TryGetProperty("participantUserIds", out var participants)
                        ? participants.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToList()
                        : [],
                    root.TryGetProperty("evidenceCount", out var evidenceCount) ? evidenceCount.GetInt32() : 0,
                    root.TryGetProperty("conflictState", out var conflict) ? conflict.GetString() ?? "unknown" : "unknown",
                    root.TryGetProperty("outcomeSummary", out var outcome) ? outcome.GetString() ?? "Outcome not recorded." : "Outcome not recorded.");
            }
            catch (JsonException)
            {
                return ("Decision", "draft", [], 0, "unknown", "Outcome not recorded.");
            }
        }
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks explorers permission.");
        }

        return context;
    }
}

public sealed class ArtifactExplorerService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder) : IArtifactExplorerService
{
    public async Task<IReadOnlyCollection<ArtifactExplorerSummaryResponse>> ListArtifactsAsync(
        string? artifactType,
        string? lifecycleState,
        string? search,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionsAsync(
            "explorers.artifacts.list",
            [ExplorerPermissions.Read, ArtifactPermissions.Read],
            cancellationToken);

        var query = dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId);

        if (!string.IsNullOrWhiteSpace(artifactType))
        {
            var normalizedType = artifactType.Trim().ToUpperInvariant();
            query = query.Where(artifact => artifact.NormalizedArtifactType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(lifecycleState)
            && Enum.TryParse<ArtifactLifecycleState>(lifecycleState, true, out var parsedLifecycle))
        {
            query = query.Where(artifact => artifact.LifecycleState == parsedLifecycle);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(artifact =>
                artifact.Name.Contains(trimmedSearch)
                || (artifact.Description != null && artifact.Description.Contains(trimmedSearch)));
        }

        var artifacts = await query
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(artifact => artifact.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        return artifacts.Select(artifact =>
        {
            versionLookup.TryGetValue(artifact.Id, out var version);
            return new ArtifactExplorerSummaryResponse(
                artifact.Id,
                artifact.ArtifactType,
                artifact.Name,
                artifact.LifecycleState.ToString(),
                version?.VersionLabel,
                artifact.Description ?? $"Artifact '{artifact.Name}'.",
                $"/artifacts/{artifact.Id}",
                artifact.UpdatedAt);
        }).ToList();
    }

    private async Task<ActiveTenantContext> RequirePermissionsAsync(
        string action,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        foreach (var permissionKey in permissionKeys)
        {
            if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
            {
                await denialRecorder.RecordAsync(
                    context.TenantId,
                    context.UserId,
                    action,
                    "permission_denied",
                    $"The user lacks the {permissionKey} permission.",
                    cancellationToken);
                throw new TenantAccessDeniedException($"User lacks {permissionKey} permission.");
            }
        }

        return context;
    }
}
