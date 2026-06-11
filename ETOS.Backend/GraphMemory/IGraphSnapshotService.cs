namespace ETOS.Backend.GraphMemory;

public interface IGraphSnapshotService
{
    Task<GraphSnapshotContract> CaptureAsync(Guid tenantId, GraphSpace graphSpace, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GraphSnapshotContract>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<GraphSnapshotContract> GetAsync(Guid tenantId, Guid snapshotId, CancellationToken cancellationToken);
}
