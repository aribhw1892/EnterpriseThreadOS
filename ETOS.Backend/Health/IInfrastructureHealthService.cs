namespace ETOS.Backend.Health;

public interface IInfrastructureHealthService
{
    Task<IReadOnlyCollection<ComponentHealthResponse>> CheckAsync(CancellationToken cancellationToken);
}
