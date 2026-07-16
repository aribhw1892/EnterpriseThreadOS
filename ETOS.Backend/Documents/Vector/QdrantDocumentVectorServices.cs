using System.Text.Json;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ETOS.Backend.Documents.Vector;

public sealed class QdrantDocumentVectorIndexingService(
    IOptions<DocumentVectorIndexingOptions> options,
    IEmbeddingProvider embeddingProvider) : IDocumentVectorIndexingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DocumentVectorIndexingOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled;

    public async Task<DocumentVectorIndexResult> RequestIndexAsync(
        DocumentVectorIndexContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new DocumentVectorIndexResult(
                DocumentVectorIndexStatus.DisabledPlaceholder,
                "disabled-qdrant-placeholder",
                "Qdrant indexing provider is not enabled.");
        }

        var text = BuildIndexableText(context);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentVectorIndexResult(
                DocumentVectorIndexStatus.Failed,
                "qdrant-v1",
                "No extractable text was available for vector indexing.");
        }

        try
        {
            var client = CreateClient();
            await EnsureCollectionAsync(client, cancellationToken);
            var chunks = ChunkText(text, _options.Embedding.MaxChunkCharacters);
            var points = new List<PointStruct>();
            for (var index = 0; index < chunks.Count; index++)
            {
                var chunk = chunks[index];
                var vector = await embeddingProvider.EmbedAsync(chunk, cancellationToken);
                var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
                {
                    ["tenantId"] = ToPayloadValue(context.Document.TenantId.ToString()),
                    ["documentArtifactId"] = ToPayloadValue(context.Document.Id.ToString()),
                    ["documentVersionId"] = ToPayloadValue(context.Version.Id.ToString()),
                    ["classificationKey"] = ToPayloadValue(context.Document.ClassificationKey),
                    ["documentType"] = ToPayloadValue(context.Document.DocumentType),
                    ["title"] = ToPayloadValue(context.Document.Title),
                    ["versionLabel"] = ToPayloadValue(context.Version.VersionLabel),
                    ["safeSummary"] = ToPayloadValue(chunk.Length > 500 ? chunk[..500] : chunk),
                    ["chunkIndex"] = ToPayloadValue(index),
                    ["graphNodeIds"] = ToPayloadValue(JsonSerializer.Serialize(context.LinkedGraphNodeIds, JsonOptions))
                };

                points.Add(new PointStruct
                {
                    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                    Vectors = vector.ToArray(),
                    Payload = { payload }
                });
            }

            await client.UpsertAsync(
                _options.Qdrant.CollectionName,
                points,
                cancellationToken: cancellationToken);

            return new DocumentVectorIndexResult(
                DocumentVectorIndexStatus.Indexed,
                "qdrant-v1",
                null);
        }
        catch (Exception exception)
        {
            return new DocumentVectorIndexResult(
                DocumentVectorIndexStatus.Failed,
                "qdrant-v1",
                exception.Message);
        }
    }

    private QdrantClient CreateClient()
    {
        return new QdrantClient(
            host: _options.Qdrant.Host,
            port: _options.Qdrant.Port,
            https: false);
    }

    private async Task EnsureCollectionAsync(QdrantClient client, CancellationToken cancellationToken)
    {
        var exists = await client.CollectionExistsAsync(_options.Qdrant.CollectionName, cancellationToken);
        if (exists)
        {
            return;
        }

        await client.CreateCollectionAsync(
            _options.Qdrant.CollectionName,
            new VectorParams
            {
                Size = (ulong)embeddingProvider.Dimensions,
                Distance = Distance.Cosine
            },
            cancellationToken: cancellationToken);
    }

    private static string BuildIndexableText(DocumentVectorIndexContext context)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.ExtractedText))
        {
            parts.Add(context.ExtractedText);
        }

        if (!string.IsNullOrWhiteSpace(context.Version.ExtractedMetadataSummaryJson))
        {
            parts.Add(context.Version.ExtractedMetadataSummaryJson);
        }

        parts.Add($"Document '{context.Document.Title}' version '{context.Version.VersionLabel}'.");
        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static List<string> ChunkText(string text, int maxChunkCharacters)
    {
        if (text.Length <= maxChunkCharacters)
        {
            return [text];
        }

        var chunks = new List<string>();
        for (var offset = 0; offset < text.Length; offset += maxChunkCharacters)
        {
            var length = Math.Min(maxChunkCharacters, text.Length - offset);
            chunks.Add(text.Substring(offset, length));
        }

        return chunks;
    }

    private static Qdrant.Client.Grpc.Value ToPayloadValue(string value)
    {
        return new Qdrant.Client.Grpc.Value { StringValue = value };
    }

    private static Qdrant.Client.Grpc.Value ToPayloadValue(int value)
    {
        return new Qdrant.Client.Grpc.Value { IntegerValue = value };
    }
}

public sealed class QdrantDocumentVectorSearchService(
    IOptions<DocumentVectorIndexingOptions> options,
    IEmbeddingProvider embeddingProvider) : IDocumentVectorSearchService
{
    private readonly DocumentVectorIndexingOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled;

    public async Task<IReadOnlyCollection<DocumentVectorSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(queryText))
        {
            return [];
        }

        try
        {
            var client = new QdrantClient(
                host: _options.Qdrant.Host,
                port: _options.Qdrant.Port,
                https: false);
            if (!await client.CollectionExistsAsync(_options.Qdrant.CollectionName, cancellationToken))
            {
                return [];
            }

            var vector = (await embeddingProvider.EmbedAsync(queryText, cancellationToken)).ToArray();
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "tenantId",
                            Match = new Match { Keyword = tenantId.ToString() }
                        }
                    }
                }
            };

            var results = await client.SearchAsync(
                _options.Qdrant.CollectionName,
                vector,
                filter: filter,
                limit: (ulong)Math.Clamp(limit, 1, 20),
                cancellationToken: cancellationToken);

            return results
                .Select(point =>
                {
                    var payload = point.Payload;
                    return new DocumentVectorSearchHit(
                        Guid.Parse(payload["documentArtifactId"].StringValue),
                        Guid.Parse(payload["documentVersionId"].StringValue),
                        payload.TryGetValue("classificationKey", out var classification)
                            ? classification.StringValue
                            : "internal",
                        payload.TryGetValue("documentType", out var documentType)
                            ? documentType.StringValue
                            : "document",
                        payload.TryGetValue("safeSummary", out var summary)
                            ? summary.StringValue
                            : "Indexed document chunk.",
                        point.Score);
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
