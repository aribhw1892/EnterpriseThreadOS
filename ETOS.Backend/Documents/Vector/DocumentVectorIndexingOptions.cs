namespace ETOS.Backend.Documents.Vector;

public sealed class DocumentVectorIndexingOptions
{
    public const string SectionName = "DocumentVectorIndexing";

    public bool Enabled { get; set; }

    public bool AutoIndexOnUpload { get; set; } = true;

    public QdrantDocumentVectorOptions Qdrant { get; set; } = new();

    public DocumentEmbeddingOptions Embedding { get; set; } = new();
}

public sealed class QdrantDocumentVectorOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6334;

    public string CollectionName { get; set; } = "etos-document-chunks";

    public bool UseGrpc { get; set; } = true;
}

public sealed class DocumentEmbeddingOptions
{
    /// <summary>
    /// Supported keys: deterministic-v1, openai, openai-v1, openai-compatible
    /// (same dual-mode principle as agent runtime model providers).
    /// </summary>
    public string Provider { get; set; } = EmbeddingProviderKeys.Deterministic;

    /// <summary>
    /// Embedding model id for openai / openai-compatible providers.
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    public int Dimensions { get; set; } = 64;

    public int MaxChunkCharacters { get; set; } = 1200;

    /// <summary>
    /// Optional config override. Falls back to OPENAI_API_KEY.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional config override. Falls back to OPENAI_BASE_URL.
    /// Cloud openai defaults to https://api.openai.com/v1 when unset.
    /// </summary>
    public string? BaseUrl { get; set; }
}
