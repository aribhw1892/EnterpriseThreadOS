namespace ETOS.Backend.GraphMemory;

public sealed class MemgraphGraphMemoryService : IGraphMemoryService, IGraphHealthService, IGraphBootstrapService
{
    public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<BaseRelationship> CreateRelationshipAsync(
        CreateGraphRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<GraphReadModel> ListGraphAsync(
        Guid tenantId,
        GraphSpace? graphSpace,
        string? sourceBatchId,
        IReadOnlyCollection<Guid>? nodeIds,
        IReadOnlyCollection<Guid>? relationshipIds,
        CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<GraphPromotionCopyResult> PromoteStagingAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> stagingNodeIds,
        IReadOnlyCollection<Guid> stagingRelationshipIds,
        CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task<GraphHealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    public Task BootstrapAsync(CancellationToken cancellationToken)
    {
        throw Deferred();
    }

    private static NotSupportedException Deferred()
    {
        return new NotSupportedException("Memgraph graph memory is an optional disabled adapter placeholder for later evaluation.");
    }
}
