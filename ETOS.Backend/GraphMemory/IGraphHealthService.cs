namespace ETOS.Backend.GraphMemory;

public interface IGraphHealthService
{
    Task<GraphHealthResponse> CheckAsync(CancellationToken cancellationToken);
}
