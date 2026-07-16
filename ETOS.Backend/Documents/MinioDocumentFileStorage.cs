using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace ETOS.Backend.Documents;

public sealed class MinioDocumentFileStorage(IOptions<DocumentFileStorageOptions> options) : IDocumentFileStorage
{
    private readonly MinioDocumentStorageOptions _minio = options.Value.Minio;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketEnsured;

    public async Task<StoredDocumentFile> StoreAsync(
        Guid tenantId,
        Guid documentId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(originalFileName) ? "document.bin" : originalFileName);
        var storageKey = $"{tenantId:N}/{documentId:N}/{Guid.NewGuid():N}-{safeFileName}";

        await using var buffer = new MemoryStream();
        using var sha256 = SHA256.Create();
        var hashBuffer = new byte[81920];
        long totalBytes = 0;
        int read;
        while ((read = await content.ReadAsync(hashBuffer, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(hashBuffer.AsMemory(0, read), cancellationToken);
            sha256.TransformBlock(hashBuffer, 0, read, null, 0);
            totalBytes += read;
        }

        sha256.TransformFinalBlock([], 0, 0);
        buffer.Position = 0;

        var client = CreateClient();
        await client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_minio.Bucket)
                .WithObject(storageKey)
                .WithStreamData(buffer)
                .WithObjectSize(buffer.Length)
                .WithContentType("application/octet-stream"),
            cancellationToken);

        return new StoredDocumentFile(storageKey, Convert.ToHexString(sha256.Hash!).ToLowerInvariant(), totalBytes);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        var memory = new MemoryStream();
        var client = CreateClient();
        await client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(_minio.Bucket)
                .WithObject(storageKey)
                .WithCallbackStream(stream => stream.CopyTo(memory)),
            cancellationToken);
        memory.Position = 0;
        return memory;
    }

    private IMinioClient CreateClient()
    {
        return new MinioClient()
            .WithEndpoint(_minio.Endpoint)
            .WithCredentials(_minio.AccessKey, _minio.SecretKey)
            .WithSSL(_minio.UseSsl)
            .Build();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (_bucketEnsured)
            {
                return;
            }

            var client = CreateClient();
            var exists = await client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_minio.Bucket),
                cancellationToken);
            if (!exists)
            {
                await client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_minio.Bucket),
                    cancellationToken);
            }

            _bucketEnsured = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }
}
