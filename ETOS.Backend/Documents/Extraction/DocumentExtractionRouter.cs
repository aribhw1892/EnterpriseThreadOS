using System.Text;
using System.Text.Json.Nodes;
using ETOS.Backend.Documents;

namespace ETOS.Backend.Documents.Extraction;

public sealed class DocumentExtractionRouter(IEnumerable<IDocumentExtractionProvider> providers) : IDocumentExtractionRouter
{
    private readonly IReadOnlyList<IDocumentExtractionProvider> _providers = providers
        .OrderByDescending(provider => provider.ProviderKey != "generic-binary-v1")
        .ToList();

    public async Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var provider = _providers.FirstOrDefault(candidate => candidate.CanHandle(request.OriginalFileName, request.ContentType))
            ?? _providers.LastOrDefault(candidate => candidate.ProviderKey == "generic-binary-v1")
            ?? throw new InvalidOperationException("No document extraction providers are registered.");

        return await provider.ExtractAsync(request, cancellationToken);
    }
}

public abstract class DocumentExtractionProviderBase : IDocumentExtractionProvider
{
    public abstract string ProviderKey { get; }

    public abstract bool CanHandle(string originalFileName, string contentType);

    public abstract Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken);

    protected static string? MergeMetadata(string? uploadedMetadataSummaryJson, Action<JsonObject> enrich)
    {
        JsonObject node;
        try
        {
            node = string.IsNullOrWhiteSpace(uploadedMetadataSummaryJson)
                ? new JsonObject()
                : JsonNode.Parse(uploadedMetadataSummaryJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            node = new JsonObject();
        }

        enrich(node);
        return node.ToJsonString();
    }

    protected static async Task<string> ReadTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
