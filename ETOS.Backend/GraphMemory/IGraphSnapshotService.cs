namespace ETOS.Backend.GraphMemory;

public interface IGraphSnapshotService
{
    Task<GraphSnapshotContract> CaptureAsync(Guid tenantId, GraphSpace graphSpace, CancellationToken cancellationToken);
}
