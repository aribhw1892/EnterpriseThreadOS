using System.Security.Cryptography;

namespace ETOS.Backend.Imports;

public sealed class ImportFileStorageOptions
{
    public const string SectionName = "ImportFileStorage";

    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "import-evidence");
}

public sealed record StoredImportFile(
    string StorageKey,
    string Sha256Checksum,
    long SizeBytes);

public interface IImportFileStorage
{
    Task<StoredImportFile> StoreAsync(
        Guid tenantId,
        Guid importBatchId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed class LocalImportFileStorage(Microsoft.Extensions.Options.IOptions<ImportFileStorageOptions> options) : IImportFileStorage
{
    public async Task<StoredImportFile> StoreAsync(
        Guid tenantId,
        Guid importBatchId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(originalFileName) ? "import.csv" : originalFileName);
        var storageKey = Path.Combine(tenantId.ToString("N"), importBatchId.ToString("N"), $"{Guid.NewGuid():N}-{safeFileName}");
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
        return new StoredImportFile(storageKey, Convert.ToHexString(sha256.Hash!).ToLowerInvariant(), totalBytes);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = GetAbsolutePath(storageKey);
        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }

    private string GetAbsolutePath(string storageKey)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        var absolutePath = Path.GetFullPath(Path.Combine(root, storageKey));
        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Import evidence storage key escaped the configured root path.");
        }

        return absolutePath;
    }
}
