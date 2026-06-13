namespace ETOS.Backend.GovernedChat.Llm;

public sealed class GovernedChatLlmOptions
{
    public const string SectionName = "GovernedChat";

    public string LlmProvider { get; set; } = "Deterministic";

    public string? OpenAiApiKey { get; set; }

    public string OpenAiModel { get; set; } = "gpt-4o-mini";
}
