using System.Security.Cryptography;
using System.Text;

namespace ETOS.Backend.Documents.Vector;

public interface IEmbeddingProvider
{
    string ProviderKey { get; }

    int Dimensions { get; }

    Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken);
}

public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    public DeterministicEmbeddingProvider(int dimensions)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions));
        }

        Dimensions = dimensions;
    }

    public string ProviderKey => "deterministic-v1";

    public int Dimensions { get; }

    public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        var vector = new float[Dimensions];
        for (var index = 0; index < Dimensions; index++)
        {
            vector[index] = bytes[index % bytes.Length] / 255f;
        }

        return Task.FromResult<IReadOnlyList<float>>(vector);
    }
}
