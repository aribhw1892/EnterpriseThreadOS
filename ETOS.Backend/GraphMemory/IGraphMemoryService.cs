namespace ETOS.Backend.GraphMemory;

public interface IGraphMemoryService
{
    Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken);

    Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken);

    Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken);

    Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken);

    Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken);
}
