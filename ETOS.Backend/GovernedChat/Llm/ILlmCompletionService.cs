namespace ETOS.Backend.GovernedChat.Llm;

public interface ILlmCompletionService
{
    string ProviderName { get; }

    Task<string> CompleteStructuredAsync(
        string prompt,
        string outputSchemaJson,
        CancellationToken cancellationToken);
}
