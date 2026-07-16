using ETOS.Backend.Documents;

namespace ETOS.Backend.Documents.Extraction;

public sealed class SolidWorksMetadataDocumentExtractionProvider : DocumentExtractionProviderBase
{
    private static readonly string[] SupportedExtensions = [".sldprt", ".sldasm", ".slddrw"];

    public override string ProviderKey => "solidworks-metadata-v1";

    public override bool CanHandle(string originalFileName, string contentType)
    {
        var extension = Path.GetExtension(originalFileName);
        return SupportedExtensions.Any(item => extension.Equals(item, StringComparison.OrdinalIgnoreCase));
    }

    public override Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safeSummary =
            $"SolidWorks metadata-only extraction for '{request.OriginalFileName}'. Geometry parsing remains disabled.";
        return Task.FromResult(new DocumentExtractionResult(
            ProviderKey,
            DocumentExtractionStatus.MetadataImported,
            safeSummary,
            MergeMetadata(request.UploadedMetadataSummaryJson, node =>
            {
                node["extractionProvider"] = ProviderKey;
                node["originalFileName"] = request.OriginalFileName;
                node["cadGeometryParsing"] = "disabled-placeholder";
                node["contentType"] = request.ContentType;
            }),
            null));
    }
}
