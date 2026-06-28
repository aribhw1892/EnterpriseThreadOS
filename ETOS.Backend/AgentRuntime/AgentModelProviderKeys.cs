using ETOS.Backend.Identity;

namespace ETOS.Backend.AgentRuntime;

public static class AgentModelProviderKeys
{
    public static readonly IReadOnlyCollection<string> All =
    [
        "openai",
        "openai-v1",
        "openai-compatible"
    ];

    public static void Validate(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new RequestValidationException("primaryModelProviderKey is required.");
        }

        if (!All.Contains(providerKey.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"primaryModelProviderKey '{providerKey}' is not supported. Allowed values: {string.Join(", ", All)}.");
        }
    }
}
