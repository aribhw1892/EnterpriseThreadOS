namespace ETOS.Backend.Documents.Extraction;

using ETOS.Backend.Documents;

public sealed record DocumentExtractionRequest(
    string OriginalFileName,
    string ContentType,
    Stream Content,
    string? UploadedMetadataSummaryJson);

public sealed record DocumentExtractionResult(
    string ProviderKey,
    DocumentExtractionStatus ExtractionStatus,
    string? ExtractedText,
    string? ExtractedMetadataSummaryJson,
    string? FailureSummary);

public interface IDocumentExtractionProvider
{
    string ProviderKey { get; }

    bool CanHandle(string originalFileName, string contentType);

    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken);
}

public interface IDocumentExtractionRouter
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken);
}
