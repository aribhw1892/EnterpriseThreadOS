using ETOS.Backend.Documents;

namespace ETOS.Backend.Documents.Extraction;

public sealed class TextDocumentExtractionProvider : DocumentExtractionProviderBase
{
    public override string ProviderKey => "text-v1";

    public override bool CanHandle(string originalFileName, string contentType)
    {
        var extension = Path.GetExtension(originalFileName);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var text = await ReadTextAsync(request.Content, cancellationToken);
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return new DocumentExtractionResult(
                ProviderKey,
                DocumentExtractionStatus.Uncertain,
                null,
                MergeMetadata(request.UploadedMetadataSummaryJson, node =>
                {
                    node["extractionProvider"] = ProviderKey;
                    node["originalFileName"] = request.OriginalFileName;
                }),
                "Text extraction produced no readable content.");
        }

        return new DocumentExtractionResult(
            ProviderKey,
            DocumentExtractionStatus.Completed,
            trimmed.Length > 8000 ? trimmed[..8000] : trimmed,
            MergeMetadata(request.UploadedMetadataSummaryJson, node =>
            {
                node["extractionProvider"] = ProviderKey;
                node["originalFileName"] = request.OriginalFileName;
                node["extractedCharCount"] = trimmed.Length;
            }),
            null);
    }
}
