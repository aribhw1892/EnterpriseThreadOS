namespace ETOS.Backend.GraphMemory;

public sealed class DeferredGraphDiffService : IGraphDiffService
{
    public Task<GraphDiffContract> CreateDiffAsync(
        Guid tenantId,
        Guid fromSnapshotId,
        Guid toSnapshotId,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Graph diff generation is defined as a contract in Slice 6 and implemented in Issue 11.");
    }
}
