namespace ETOS.Backend.Documents.Vector;

public sealed record DocumentVectorSearchHit(
    Guid DocumentArtifactId,
    Guid DocumentVersionId,
    string ClassificationKey,
    string DocumentType,
    string SafeSummary,
    float Score);

public interface IDocumentVectorSearchService
{
    bool IsEnabled { get; }

    Task<IReadOnlyCollection<DocumentVectorSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class DisabledDocumentVectorSearchService : IDocumentVectorSearchService
{
    public bool IsEnabled => false;

    public Task<IReadOnlyCollection<DocumentVectorSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<DocumentVectorSearchHit>>([]);
    }
}
