namespace ETOS.Backend.GraphMemory;

public interface IGraphDiffService
{
    Task<GraphDiffContract> CreateDiffAsync(Guid tenantId, Guid fromSnapshotId, Guid toSnapshotId, CancellationToken cancellationToken);

    Task<GraphDiffContract> GetAsync(Guid tenantId, Guid diffId, CancellationToken cancellationToken);
}
