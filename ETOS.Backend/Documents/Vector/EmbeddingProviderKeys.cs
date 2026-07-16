namespace ETOS.Backend.Documents.Vector;

public static class EmbeddingProviderKeys
{
    public const string Deterministic = "deterministic-v1";

    public const string OpenAi = "openai";

    public const string OpenAiV1 = "openai-v1";

    public const string OpenAiCompatible = "openai-compatible";

    public static readonly IReadOnlyCollection<string> All =
    [
        Deterministic,
        OpenAi,
        OpenAiV1,
        OpenAiCompatible
    ];

    public static string Normalize(string? providerKey)
        => string.IsNullOrWhiteSpace(providerKey)
            ? Deterministic
            : providerKey.Trim().ToLowerInvariant();

    public static bool IsOpenAiFamily(string providerKey)
    {
        var normalized = Normalize(providerKey);
        return normalized is OpenAi or OpenAiV1 or OpenAiCompatible;
    }

    public static void Validate(string? providerKey)
    {
        var normalized = Normalize(providerKey);
        if (!All.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Embedding provider '{providerKey}' is not supported. Allowed values: {string.Join(", ", All)}.");
        }
    }
}
