namespace ETOS.Backend.ToolRegistry;

public interface IToolExecutionQueue
{
    Task EnqueueAsync(Guid toolRunId, CancellationToken cancellationToken);
}

public sealed class DisabledToolExecutionQueue : IToolExecutionQueue
{
    public Task EnqueueAsync(Guid toolRunId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Async tool execution via MassTransit is not enabled in MVP.");
}
