using ETOS.Backend.Documents;
using UglyToad.PdfPig;

namespace ETOS.Backend.Documents.Extraction;

public sealed class PdfTextDocumentExtractionProvider : DocumentExtractionProviderBase
{
    public override string ProviderKey => "pdf-text-v1";

    public override bool CanHandle(string originalFileName, string contentType)
    {
        return Path.GetExtension(originalFileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    public override Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var document = PdfDocument.Open(request.Content);
            var pages = document.GetPages()
                .Select(page => page.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text));
            var text = string.Join(Environment.NewLine, pages).Trim();
            if (text.Length == 0)
            {
                return Task.FromResult(new DocumentExtractionResult(
                    ProviderKey,
                    DocumentExtractionStatus.Uncertain,
                    null,
                    MergeMetadata(request.UploadedMetadataSummaryJson, node =>
                    {
                        node["extractionProvider"] = ProviderKey;
                        node["originalFileName"] = request.OriginalFileName;
                    }),
                    "PDF text layer extraction produced no readable content."));
            }

            return Task.FromResult(new DocumentExtractionResult(
                ProviderKey,
                DocumentExtractionStatus.Completed,
                text.Length > 8000 ? text[..8000] : text,
                MergeMetadata(request.UploadedMetadataSummaryJson, node =>
                {
                    node["extractionProvider"] = ProviderKey;
                    node["originalFileName"] = request.OriginalFileName;
                    node["extractedCharCount"] = text.Length;
                }),
                null));
        }
        catch (Exception exception)
        {
            return Task.FromResult(new DocumentExtractionResult(
                ProviderKey,
                DocumentExtractionStatus.Failed,
                null,
                MergeMetadata(request.UploadedMetadataSummaryJson, node =>
                {
                    node["extractionProvider"] = ProviderKey;
                    node["originalFileName"] = request.OriginalFileName;
                }),
                $"PDF extraction failed: {exception.Message}"));
        }
    }
}
