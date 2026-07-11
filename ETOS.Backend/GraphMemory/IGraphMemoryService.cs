namespace ETOS.Backend.GraphMemory;

public interface IGraphMemoryService
{
    Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken);

    Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken);

    Task<BaseNode?> FindNodeByIdentityAsync(
        Guid tenantId,
        GraphSpace graphSpace,
        string identityKey,
        CancellationToken cancellationToken)
        => Task.FromResult<BaseNode?>(null);

    Task<BaseNode> EnsureNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
        => CreateNodeAsync(request, cancellationToken);

    Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken);

    Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken);

    Task<BaseRelationship> EnsureRelationshipAsync(
        CreateGraphRelationshipRequest request,
        CancellationToken cancellationToken)
        => CreateRelationshipAsync(request, cancellationToken);

    Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken);

    Task<GraphReadModel> ListGraphAsync(
        Guid tenantId,
        GraphSpace? graphSpace,
        string? sourceBatchId,
        IReadOnlyCollection<Guid>? nodeIds,
        IReadOnlyCollection<Guid>? relationshipIds,
        CancellationToken cancellationToken);

    Task<GraphPromotionCopyResult> PromoteStagingAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> stagingNodeIds,
        IReadOnlyCollection<Guid> stagingRelationshipIds,
        CancellationToken cancellationToken);
}
