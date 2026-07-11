using System.Text.Json;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ETOS.Backend.GraphMemory;

public sealed class Neo4jGraphMemoryService(IDriver driver, IOptions<GraphMemoryOptions> options) : IGraphMemoryService
{
    private const int MaximumTraversalDepth = 5;
    private const int TraversalRowLimit = 250;
    private const string FlatAttributePrefix = "attr_";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
    {
        ValidateTenant(request.TenantId);
        ValidateRequired(request.ObjectType, nameof(request.ObjectType));

        var now = DateTimeOffset.UtcNow;
        var nodeId = Guid.NewGuid();
        var parameters = new Dictionary<string, object?>
        {
            ["nodeId"] = nodeId.ToString(),
            ["tenantId"] = request.TenantId.ToString(),
            ["graphSpace"] = request.GraphSpace.ToString(),
            ["objectType"] = request.ObjectType,
            ["trustState"] = request.TrustState.ToString(),
            ["attributesJson"] = SerializeAttributes(request.Attributes),
            ["identityKey"] = request.IdentityKey,
            ["sourceSystem"] = request.SourceReference?.SourceSystem,
            ["sourceRecordId"] = request.SourceReference?.SourceRecordId,
            ["sourceBatchId"] = request.SourceReference?.SourceBatchId,
            ["createdAt"] = now.ToString("O"),
            ["updatedAt"] = now.ToString("O"),
            ["flatAttributes"] = BuildFlatAttributeProperties(request.Attributes)
        };

        await using var session = OpenSession();
        var cursor = await session.RunAsync(
            """
            CREATE (node:BaseNode:EtosNode {
                nodeId: $nodeId,
                tenantId: $tenantId,
                graphSpace: $graphSpace,
                objectType: $objectType,
                trustState: $trustState,
                attributesJson: $attributesJson,
                identityKey: $identityKey,
                sourceSystem: $sourceSystem,
                sourceRecordId: $sourceRecordId,
                sourceBatchId: $sourceBatchId,
                createdAt: $createdAt,
                updatedAt: $updatedAt
            })
            SET node += $flatAttributes
            RETURN node
            """,
            parameters);

        var record = await cursor.SingleAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return MapNode(record["node"].As<INode>());
    }

    public async Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        ValidateIdentifier(nodeId, nameof(nodeId));

        await using var session = OpenSession();
        var cursor = await session.RunAsync(
            """
            MATCH (node:BaseNode { tenantId: $tenantId, nodeId: $nodeId })
            RETURN node
            LIMIT 1
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = tenantId.ToString(),
                ["nodeId"] = nodeId.ToString()
            });

        var records = await cursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return records.Count == 0 ? null : MapNode(records[0]["node"].As<INode>());
    }

    public async Task<BaseNode?> FindNodeByIdentityAsync(
        Guid tenantId,
        GraphSpace graphSpace,
        string identityKey,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        ValidateRequired(identityKey, nameof(identityKey));

        await using var session = OpenSession();
        var cursor = await session.RunAsync(
            """
            MATCH (node:BaseNode { tenantId: $tenantId, graphSpace: $graphSpace, identityKey: $identityKey })
            RETURN node
            LIMIT 1
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = tenantId.ToString(),
                ["graphSpace"] = graphSpace.ToString(),
                ["identityKey"] = identityKey
            });

        var records = await cursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return records.Count == 0 ? null : MapNode(records[0]["node"].As<INode>());
    }

    public async Task<BaseNode> EnsureNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityKey))
        {
            return await CreateNodeAsync(request, cancellationToken);
        }

        var existing = await FindNodeByIdentityAsync(
            request.TenantId,
            request.GraphSpace,
            request.IdentityKey,
            cancellationToken);
        if (existing is null)
        {
            return await CreateNodeAsync(request, cancellationToken);
        }

        return await UpdateNodeAsync(
            new UpdateGraphNodeRequest(
                request.TenantId,
                existing.NodeId,
                request.TrustState,
                MergeAttributes(existing.Attributes, request.Attributes),
                request.SourceReference),
            cancellationToken);
    }

    public async Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken)
    {
        ValidateTenant(request.TenantId);
        ValidateIdentifier(request.NodeId, nameof(request.NodeId));

        var now = DateTimeOffset.UtcNow;
        await using var session = OpenSession();
        var cursor = await session.RunAsync(
            """
            MATCH (node:BaseNode { tenantId: $tenantId, nodeId: $nodeId })
            SET node.trustState = coalesce($trustState, node.trustState),
                node.attributesJson = coalesce($attributesJson, node.attributesJson),
                node.sourceSystem = coalesce($sourceSystem, node.sourceSystem),
                node.sourceRecordId = coalesce($sourceRecordId, node.sourceRecordId),
                node.sourceBatchId = coalesce($sourceBatchId, node.sourceBatchId),
                node.updatedAt = $updatedAt
            SET node += $flatAttributes
            RETURN node
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["nodeId"] = request.NodeId.ToString(),
                ["trustState"] = request.TrustState?.ToString(),
                ["attributesJson"] = request.Attributes is null ? null : SerializeAttributes(request.Attributes),
                ["sourceSystem"] = request.SourceReference?.SourceSystem,
                ["sourceRecordId"] = request.SourceReference?.SourceRecordId,
                ["sourceBatchId"] = request.SourceReference?.SourceBatchId,
                ["updatedAt"] = now.ToString("O"),
                ["flatAttributes"] = BuildFlatAttributeProperties(request.Attributes)
            });

        var records = await cursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Graph node was not found for the current tenant.");
        }

        return MapNode(records[0]["node"].As<INode>());
    }

    public async Task<BaseRelationship> CreateRelationshipAsync(
        CreateGraphRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTenant(request.TenantId);
        ValidateIdentifier(request.FromNodeId, nameof(request.FromNodeId));
        ValidateIdentifier(request.ToNodeId, nameof(request.ToNodeId));
        ValidateRequired(request.RelationshipType, nameof(request.RelationshipType));

        var now = DateTimeOffset.UtcNow;
        var relationshipId = Guid.NewGuid();
        await using var session = OpenSession();
        var cursor = await session.RunAsync(
            """
            MATCH (from:BaseNode { tenantId: $tenantId, nodeId: $fromNodeId })
            MATCH (to:BaseNode { tenantId: $tenantId, nodeId: $toNodeId })
            CREATE (from)-[relationship:BASE_RELATIONSHIP {
                relationshipId: $relationshipId,
                tenantId: $tenantId,
                relationshipType: $relationshipType,
                trustState: $trustState,
                attributesJson: $attributesJson,
                sourceSystem: $sourceSystem,
                sourceRecordId: $sourceRecordId,
                sourceBatchId: $sourceBatchId,
                createdAt: $createdAt,
                updatedAt: $updatedAt
            }]->(to)
            SET relationship += $flatAttributes
            RETURN relationship, from.nodeId AS fromNodeId, to.nodeId AS toNodeId
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["fromNodeId"] = request.FromNodeId.ToString(),
                ["toNodeId"] = request.ToNodeId.ToString(),
                ["relationshipId"] = relationshipId.ToString(),
                ["relationshipType"] = request.RelationshipType,
                ["trustState"] = request.TrustState.ToString(),
                ["attributesJson"] = SerializeAttributes(request.Attributes),
                ["sourceSystem"] = request.SourceReference?.SourceSystem,
                ["sourceRecordId"] = request.SourceReference?.SourceRecordId,
                ["sourceBatchId"] = request.SourceReference?.SourceBatchId,
                ["createdAt"] = now.ToString("O"),
                ["updatedAt"] = now.ToString("O"),
                ["flatAttributes"] = BuildFlatAttributeProperties(request.Attributes)
            });

        var records = await cursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Both relationship endpoints must exist in the current tenant.");
        }

        return MapRelationship(
            records[0]["relationship"].As<IRelationship>(),
            Guid.Parse(records[0]["fromNodeId"].As<string>()),
            Guid.Parse(records[0]["toNodeId"].As<string>()));
    }

    public async Task<BaseRelationship> EnsureRelationshipAsync(
        CreateGraphRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTenant(request.TenantId);
        ValidateIdentifier(request.FromNodeId, nameof(request.FromNodeId));
        ValidateIdentifier(request.ToNodeId, nameof(request.ToNodeId));
        ValidateRequired(request.RelationshipType, nameof(request.RelationshipType));

        await using var session = OpenSession();
        var cursor = await session.RunAsync(
            """
            MATCH (from:BaseNode { tenantId: $tenantId, nodeId: $fromNodeId })
            MATCH (to:BaseNode { tenantId: $tenantId, nodeId: $toNodeId })
            MATCH (from)-[relationship:BASE_RELATIONSHIP {
                tenantId: $tenantId,
                relationshipType: $relationshipType
            }]->(to)
            RETURN relationship, from.nodeId AS fromNodeId, to.nodeId AS toNodeId
            LIMIT 1
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["fromNodeId"] = request.FromNodeId.ToString(),
                ["toNodeId"] = request.ToNodeId.ToString(),
                ["relationshipType"] = request.RelationshipType
            });

        var records = await cursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (records.Count > 0)
        {
            var existing = MapRelationship(
                records[0]["relationship"].As<IRelationship>(),
                Guid.Parse(records[0]["fromNodeId"].As<string>()),
                Guid.Parse(records[0]["toNodeId"].As<string>()));
            var now = DateTimeOffset.UtcNow;
            var mergedAttributes = MergeAttributes(existing.Attributes, request.Attributes);
            var updateCursor = await session.RunAsync(
                """
                MATCH ()-[relationship:BASE_RELATIONSHIP { tenantId: $tenantId, relationshipId: $relationshipId }]->()
                SET relationship.trustState = coalesce($trustState, relationship.trustState),
                    relationship.attributesJson = coalesce($attributesJson, relationship.attributesJson),
                    relationship.sourceSystem = coalesce($sourceSystem, relationship.sourceSystem),
                    relationship.sourceRecordId = coalesce($sourceRecordId, relationship.sourceRecordId),
                    relationship.sourceBatchId = coalesce($sourceBatchId, relationship.sourceBatchId),
                    relationship.updatedAt = $updatedAt
                SET relationship += $flatAttributes
                RETURN relationship, $fromNodeId AS fromNodeId, $toNodeId AS toNodeId
                """,
                new Dictionary<string, object?>
                {
                    ["tenantId"] = request.TenantId.ToString(),
                    ["relationshipId"] = existing.RelationshipId.ToString(),
                    ["fromNodeId"] = request.FromNodeId.ToString(),
                    ["toNodeId"] = request.ToNodeId.ToString(),
                    ["trustState"] = request.TrustState.ToString(),
                    ["attributesJson"] = SerializeAttributes(mergedAttributes),
                    ["sourceSystem"] = request.SourceReference?.SourceSystem,
                    ["sourceRecordId"] = request.SourceReference?.SourceRecordId,
                    ["sourceBatchId"] = request.SourceReference?.SourceBatchId,
                    ["updatedAt"] = now.ToString("O"),
                    ["flatAttributes"] = BuildFlatAttributeProperties(mergedAttributes)
                });
            var updateRecords = await updateCursor.ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return MapRelationship(
                updateRecords[0]["relationship"].As<IRelationship>(),
                Guid.Parse(updateRecords[0]["fromNodeId"].As<string>()),
                Guid.Parse(updateRecords[0]["toNodeId"].As<string>()));
        }

        return await CreateRelationshipAsync(request, cancellationToken);
    }

    public async Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
    {
        ValidateTenant(request.TenantId);
        ValidateIdentifier(request.StartNodeId, nameof(request.StartNodeId));

        var maxDepth = Math.Clamp(request.MaxDepth, 1, MaximumTraversalDepth);
        var startNode = await GetNodeAsync(request.TenantId, request.StartNodeId, cancellationToken)
            ?? throw new InvalidOperationException("Traversal start node was not found for the current tenant.");

        var nodes = new Dictionary<Guid, BaseNode> { [startNode.NodeId] = startNode };
        var relationships = new Dictionary<Guid, BaseRelationship>();

        await using var session = OpenSession();
        var traversalQuery =
            "MATCH path = (start:BaseNode { tenantId: $tenantId, nodeId: $startNodeId })" +
            $"-[pathRelationships:BASE_RELATIONSHIP*1..{maxDepth}]->" +
            "(node:BaseNode { tenantId: $tenantId }) " +
            "WHERE all(pathNode IN nodes(path) WHERE pathNode.tenantId = $tenantId) " +
            "AND all(relationship IN pathRelationships WHERE relationship.tenantId = $tenantId) " +
            "AND ($graphSpace IS NULL OR all(pathNode IN nodes(path) WHERE pathNode.graphSpace = $graphSpace)) " +
            "AND (size($relationshipTypes) = 0 OR all(relationship IN pathRelationships WHERE relationship.relationshipType IN $relationshipTypes)) " +
            "AND (size($allowedTrustStates) = 0 OR (" +
            "all(pathNode IN nodes(path) WHERE pathNode.trustState IN $allowedTrustStates) " +
            "AND all(relationship IN pathRelationships WHERE relationship.trustState IN $allowedTrustStates))) " +
            "RETURN nodes(path) AS pathNodes, relationships(path) AS pathRelationships " +
            $"LIMIT {TraversalRowLimit}";
        var cursor = await session.RunAsync(
            traversalQuery,
            new Dictionary<string, object?>
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["startNodeId"] = request.StartNodeId.ToString(),
                ["graphSpace"] = request.GraphSpace?.ToString(),
                ["relationshipTypes"] = request.RelationshipTypes?.ToArray() ?? [],
                ["allowedTrustStates"] = request.AllowedTrustStates?.Select(trustState => trustState.ToString()).ToArray() ?? []
            });

        var records = await cursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var record in records)
        {
            var pathNodes = record["pathNodes"].As<List<INode>>();
            var pathRelationships = record["pathRelationships"].As<List<IRelationship>>();

            foreach (var node in pathNodes.Select(MapNode))
            {
                nodes[node.NodeId] = node;
            }

            for (var index = 0; index < pathRelationships.Count; index++)
            {
                var fromNode = MapNode(pathNodes[index]);
                var toNode = MapNode(pathNodes[index + 1]);
                var relationship = MapRelationship(pathRelationships[index], fromNode.NodeId, toNode.NodeId);
                relationships[relationship.RelationshipId] = relationship;
            }
        }

        return new GraphTraversalResult(startNode, nodes.Values.ToArray(), relationships.Values.ToArray());
    }

    public async Task<GraphReadModel> ListGraphAsync(
        Guid tenantId,
        GraphSpace? graphSpace,
        string? sourceBatchId,
        IReadOnlyCollection<Guid>? nodeIds,
        IReadOnlyCollection<Guid>? relationshipIds,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var nodeIdStrings = nodeIds?.Select(id => id.ToString()).ToArray() ?? [];
        var relationshipIdStrings = relationshipIds?.Select(id => id.ToString()).ToArray() ?? [];
        await using var session = OpenSession();

        var nodeCursor = await session.RunAsync(
            """
            MATCH (node:BaseNode { tenantId: $tenantId })
            WHERE ($graphSpace IS NULL OR node.graphSpace = $graphSpace)
              AND ($sourceBatchId IS NULL OR node.sourceBatchId = $sourceBatchId)
              AND (size($nodeIds) = 0 OR node.nodeId IN $nodeIds)
            RETURN node
            ORDER BY node.nodeId
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = tenantId.ToString(),
                ["graphSpace"] = graphSpace?.ToString(),
                ["sourceBatchId"] = sourceBatchId,
                ["nodeIds"] = nodeIdStrings
            });
        var nodeRecords = await nodeCursor.ToListAsync();

        var relationshipCursor = await session.RunAsync(
            """
            MATCH (from:BaseNode { tenantId: $tenantId })-[relationship:BASE_RELATIONSHIP { tenantId: $tenantId }]->(to:BaseNode { tenantId: $tenantId })
            WHERE ($graphSpace IS NULL OR (from.graphSpace = $graphSpace AND to.graphSpace = $graphSpace))
              AND ($sourceBatchId IS NULL OR relationship.sourceBatchId = $sourceBatchId)
              AND (size($relationshipIds) = 0 OR relationship.relationshipId IN $relationshipIds)
            RETURN relationship, from.nodeId AS fromNodeId, to.nodeId AS toNodeId
            ORDER BY relationship.relationshipId
            """,
            new Dictionary<string, object?>
            {
                ["tenantId"] = tenantId.ToString(),
                ["graphSpace"] = graphSpace?.ToString(),
                ["sourceBatchId"] = sourceBatchId,
                ["relationshipIds"] = relationshipIdStrings
            });
        var relationshipRecords = await relationshipCursor.ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return new GraphReadModel(
            nodeRecords.Select(record => MapNode(record["node"].As<INode>())).ToList(),
            relationshipRecords.Select(record => MapRelationship(
                record["relationship"].As<IRelationship>(),
                Guid.Parse(record["fromNodeId"].As<string>()),
                Guid.Parse(record["toNodeId"].As<string>()))).ToList());
    }

    public async Task<GraphPromotionCopyResult> PromoteStagingAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> stagingNodeIds,
        IReadOnlyCollection<Guid> stagingRelationshipIds,
        CancellationToken cancellationToken)
    {
        var staging = await ListGraphAsync(tenantId, GraphSpace.Staging, null, stagingNodeIds, stagingRelationshipIds, cancellationToken);
        var nodeMap = new Dictionary<Guid, Guid>();
        var trustedNodeIds = new HashSet<Guid>();
        foreach (var node in staging.Nodes.OrderBy(item => item.NodeId))
        {
            var promoted = string.IsNullOrWhiteSpace(node.IdentityKey)
                ? await CreateNodeAsync(
                    new CreateGraphNodeRequest(
                        tenantId,
                        GraphSpace.Trusted,
                        node.ObjectType,
                        TrustState.Trusted,
                        node.Attributes,
                        node.SourceReference),
                    cancellationToken)
                : await EnsureNodeAsync(
                    new CreateGraphNodeRequest(
                        tenantId,
                        GraphSpace.Trusted,
                        node.ObjectType,
                        TrustState.Trusted,
                        node.Attributes,
                        node.SourceReference,
                        node.IdentityKey),
                    cancellationToken);
            nodeMap[node.NodeId] = promoted.NodeId;
            trustedNodeIds.Add(promoted.NodeId);
        }

        var relationshipIds = new HashSet<Guid>();
        foreach (var relationship in staging.Relationships.OrderBy(item => item.RelationshipId))
        {
            if (!nodeMap.TryGetValue(relationship.FromNodeId, out var fromNodeId)
                || !nodeMap.TryGetValue(relationship.ToNodeId, out var toNodeId))
            {
                continue;
            }

            var promoted = await EnsureRelationshipAsync(
                new CreateGraphRelationshipRequest(
                    tenantId,
                    fromNodeId,
                    toNodeId,
                    relationship.RelationshipType,
                    TrustState.Trusted,
                    relationship.Attributes,
                    relationship.SourceReference),
                cancellationToken);
            relationshipIds.Add(promoted.RelationshipId);
        }

        return new GraphPromotionCopyResult(trustedNodeIds.ToList(), relationshipIds.ToList());
    }

    private IAsyncSession OpenSession()
    {
        return driver.AsyncSession(builder => builder.WithDatabase(options.Value.Neo4j.Database));
    }

    private static BaseNode MapNode(INode node)
    {
        return new BaseNode(
            Guid.Parse(GetRequiredString(node.Properties, "nodeId")),
            Guid.Parse(GetRequiredString(node.Properties, "tenantId")),
            Enum.Parse<GraphSpace>(GetRequiredString(node.Properties, "graphSpace")),
            GetRequiredString(node.Properties, "objectType"),
            Enum.Parse<TrustState>(GetRequiredString(node.Properties, "trustState")),
            DeserializeAttributes(GetOptionalString(node.Properties, "attributesJson")),
            MapSourceReference(node.Properties),
            DateTimeOffset.Parse(GetRequiredString(node.Properties, "createdAt")),
            DateTimeOffset.Parse(GetRequiredString(node.Properties, "updatedAt")),
            GetOptionalString(node.Properties, "identityKey"));
    }

    private static BaseRelationship MapRelationship(IRelationship relationship, Guid fromNodeId, Guid toNodeId)
    {
        return new BaseRelationship(
            Guid.Parse(GetRequiredString(relationship.Properties, "relationshipId")),
            Guid.Parse(GetRequiredString(relationship.Properties, "tenantId")),
            fromNodeId,
            toNodeId,
            GetRequiredString(relationship.Properties, "relationshipType"),
            Enum.Parse<TrustState>(GetRequiredString(relationship.Properties, "trustState")),
            DeserializeAttributes(GetOptionalString(relationship.Properties, "attributesJson")),
            MapSourceReference(relationship.Properties),
            DateTimeOffset.Parse(GetRequiredString(relationship.Properties, "createdAt")),
            DateTimeOffset.Parse(GetRequiredString(relationship.Properties, "updatedAt")));
    }

    private static GraphSourceReference? MapSourceReference(IReadOnlyDictionary<string, object> properties)
    {
        var sourceSystem = GetOptionalString(properties, "sourceSystem");
        var sourceRecordId = GetOptionalString(properties, "sourceRecordId");
        var sourceBatchId = GetOptionalString(properties, "sourceBatchId");

        return sourceSystem is null && sourceRecordId is null && sourceBatchId is null
            ? null
            : new GraphSourceReference(sourceSystem, sourceRecordId, sourceBatchId);
    }

    private static Dictionary<string, string?> MergeAttributes(
        IReadOnlyDictionary<string, string?> existing,
        IReadOnlyDictionary<string, string?>? incoming)
    {
        var merged = new Dictionary<string, string?>(existing, StringComparer.OrdinalIgnoreCase);
        if (incoming is null)
        {
            return merged;
        }

        foreach (var (key, value) in incoming)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    private static string SerializeAttributes(IReadOnlyDictionary<string, string?>? attributes)
    {
        return JsonSerializer.Serialize(attributes ?? new Dictionary<string, string?>(), JsonOptions);
    }

    private static Dictionary<string, object?> BuildFlatAttributeProperties(
        IReadOnlyDictionary<string, string?>? attributes)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (attributes is null)
        {
            return map;
        }

        foreach (var (key, value) in attributes)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            map[$"{FlatAttributePrefix}{key}"] = value;
        }

        return map;
    }

    private static IReadOnlyDictionary<string, string?> DeserializeAttributes(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
        {
            return new Dictionary<string, string?>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string?>>(attributesJson, JsonOptions)
            ?? new Dictionary<string, string?>();
    }

    private static string GetRequiredString(IReadOnlyDictionary<string, object> properties, string key)
    {
        var value = GetOptionalString(properties, key);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Graph value '{key}' is missing.")
            : value;
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, object> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
    }

    private static void ValidateTenant(Guid tenantId)
    {
        ValidateIdentifier(tenantId, nameof(tenantId));
    }

    private static void ValidateIdentifier(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static void ValidateRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }
}
