namespace ETOS.Backend.GraphMemory;

public interface IGraphBootstrapService
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}
