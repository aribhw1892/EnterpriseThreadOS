namespace ETOS.Backend.GraphMemory;

public sealed class DeferredGraphSnapshotService : IGraphSnapshotService
{
    public Task<GraphSnapshotContract> CaptureAsync(Guid tenantId, GraphSpace graphSpace, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Graph snapshot capture is defined as a contract in Slice 6 and implemented in Issue 11.");
    }
}
