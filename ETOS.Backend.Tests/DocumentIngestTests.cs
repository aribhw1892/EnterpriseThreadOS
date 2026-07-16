using System.Text;
using ETOS.Backend.Documents;
using ETOS.Backend.Documents.Extraction;
using ETOS.Backend.Tests.Fixtures;

namespace ETOS.Backend.Tests;

public sealed class DocumentIngestTests
{
    [Fact]
    public async Task ExtractionRouter_UsesTextProvider_ForPlainTextUpload()
    {
        var router = DocumentMemoryTestSupport.CreateExtractionRouter();
        var content = "Governed document ingest smoke text.";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await router.ExtractAsync(
            new DocumentExtractionRequest("spec.txt", "text/plain", stream, null),
            CancellationToken.None);

        Assert.Equal("text-v1", result.ProviderKey);
        Assert.Equal(DocumentExtractionStatus.Completed, result.ExtractionStatus);
        Assert.Contains("Governed document ingest", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractionRouter_UsesSolidWorksMetadataProvider_ForSldprt()
    {
        var router = DocumentMemoryTestSupport.CreateExtractionRouter();
        await using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        var result = await router.ExtractAsync(
            new DocumentExtractionRequest("bracket.sldprt", "application/octet-stream", stream, null),
            CancellationToken.None);

        Assert.Equal("solidworks-metadata-v1", result.ProviderKey);
        Assert.Equal(DocumentExtractionStatus.MetadataImported, result.ExtractionStatus);
        Assert.Contains("metadata-only", result.ExtractedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bracket.sldprt", result.ExtractedMetadataSummaryJson);
    }

    [Fact]
    public async Task ExtractionRouter_FallsBackToGenericBinary_ForUnknownExtension()
    {
        var router = DocumentMemoryTestSupport.CreateExtractionRouter();
        await using var stream = new MemoryStream([0xAA, 0xBB]);

        var result = await router.ExtractAsync(
            new DocumentExtractionRequest("archive.bin", "application/octet-stream", stream, null),
            CancellationToken.None);

        Assert.Equal("generic-binary-v1", result.ProviderKey);
        Assert.Equal(DocumentExtractionStatus.MetadataImported, result.ExtractionStatus);
        Assert.Contains("metadata-only", result.ExtractedText, StringComparison.OrdinalIgnoreCase);
    }
}
