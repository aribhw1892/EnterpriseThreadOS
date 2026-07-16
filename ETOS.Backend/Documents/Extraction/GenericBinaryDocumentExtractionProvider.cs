using ETOS.Backend.Documents;

namespace ETOS.Backend.Documents.Extraction;

public sealed class GenericBinaryDocumentExtractionProvider : DocumentExtractionProviderBase
{
    public override string ProviderKey => "generic-binary-v1";

    public override bool CanHandle(string originalFileName, string contentType) => true;

    public override Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safeSummary =
            $"Binary document '{request.OriginalFileName}' stored with metadata-only extraction fallback.";
        return Task.FromResult(new DocumentExtractionResult(
            ProviderKey,
            DocumentExtractionStatus.MetadataImported,
            safeSummary,
            MergeMetadata(request.UploadedMetadataSummaryJson, node =>
            {
                node["extractionProvider"] = ProviderKey;
                node["originalFileName"] = request.OriginalFileName;
                node["contentType"] = request.ContentType;
            }),
            null));
    }
}
