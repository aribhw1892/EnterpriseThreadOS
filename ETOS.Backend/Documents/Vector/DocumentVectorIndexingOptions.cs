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
    public string Provider { get; set; } = "deterministic-v1";

    public int Dimensions { get; set; } = 64;

    public int MaxChunkCharacters { get; set; } = 1200;
}
