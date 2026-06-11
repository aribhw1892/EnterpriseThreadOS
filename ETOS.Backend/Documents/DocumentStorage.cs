using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Documents;

public sealed class DocumentFileStorageOptions
{
    public const string SectionName = "DocumentFileStorage";

    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "document-memory");
}

public sealed record StoredDocumentFile(
    string StorageKey,
    string Sha256Checksum,
    long SizeBytes);

public interface IDocumentFileStorage
{
    Task<StoredDocumentFile> StoreAsync(
        Guid tenantId,
        Guid documentId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken);
}

public sealed class LocalDocumentFileStorage(IOptions<DocumentFileStorageOptions> options) : IDocumentFileStorage
{
    public async Task<StoredDocumentFile> StoreAsync(
        Guid tenantId,
        Guid documentId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(originalFileName) ? "document.bin" : originalFileName);
        var storageKey = Path.Combine(tenantId.ToString("N"), documentId.ToString("N"), $"{Guid.NewGuid():N}-{safeFileName}");
        var absolutePath = GetAbsolutePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var output = File.Create(absolutePath);
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        long totalBytes = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha256.TransformBlock(buffer, 0, read, null, 0);
            totalBytes += read;
        }

        sha256.TransformFinalBlock([], 0, 0);
        return new StoredDocumentFile(storageKey, Convert.ToHexString(sha256.Hash!).ToLowerInvariant(), totalBytes);
    }

    private string GetAbsolutePath(string storageKey)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        var absolutePath = Path.GetFullPath(Path.Combine(root, storageKey));
        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document storage key escaped the configured root path.");
        }

        return absolutePath;
    }
}

public interface IDocumentVectorIndexingService
{
    Task<DocumentVectorIndexStatus> RequestIndexAsync(DocumentVersion documentVersion, CancellationToken cancellationToken);
}

public sealed class DisabledDocumentVectorIndexingService : IDocumentVectorIndexingService
{
    public Task<DocumentVectorIndexStatus> RequestIndexAsync(DocumentVersion documentVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DocumentVectorIndexStatus.DisabledPlaceholder);
    }
}

public interface ICadParsingPlaceholder
{
    CadParsingPlaceholderResponse GetStatus();
}

public sealed class DisabledCadParsingPlaceholder : ICadParsingPlaceholder
{
    public CadParsingPlaceholderResponse GetStatus()
    {
        return new CadParsingPlaceholderResponse(
            false,
            "disabled-cad-geometry-parser",
            "Native CAD geometry parsing is deferred; CAD metadata can be stored as document metadata.");
    }
}
