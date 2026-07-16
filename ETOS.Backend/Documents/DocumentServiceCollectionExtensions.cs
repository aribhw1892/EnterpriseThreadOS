using ETOS.Backend.Documents.Extraction;
using ETOS.Backend.Documents.Vector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Documents;

public sealed class DocumentIngestOptions
{
    public const string SectionName = "DocumentIngest";

    public bool AutoExtractOnUpload { get; set; } = true;
}

public static class DocumentServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseThreadDocumentMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DocumentIngestOptions>()
            .Bind(configuration.GetSection(DocumentIngestOptions.SectionName));

        services.AddOptions<DocumentVectorIndexingOptions>()
            .Bind(configuration.GetSection(DocumentVectorIndexingOptions.SectionName));

        services.AddHttpClient(nameof(OpenAiCompatibleEmbeddingProvider));
        services.AddSingleton<IEmbeddingProvider>(sp =>
            EmbeddingProviderFactory.Create(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<DocumentVectorIndexingOptions>>()));

        services.AddScoped<IDocumentExtractionProvider, TextDocumentExtractionProvider>();
        services.AddScoped<IDocumentExtractionProvider, PdfTextDocumentExtractionProvider>();
        services.AddScoped<IDocumentExtractionProvider, SolidWorksMetadataDocumentExtractionProvider>();
        services.AddScoped<IDocumentExtractionProvider, GenericBinaryDocumentExtractionProvider>();
        services.AddScoped<IDocumentExtractionRouter, DocumentExtractionRouter>();

        var vectorOptions = configuration.GetSection(DocumentVectorIndexingOptions.SectionName).Get<DocumentVectorIndexingOptions>()
            ?? new DocumentVectorIndexingOptions();
        if (vectorOptions.Enabled)
        {
            services.AddScoped<IDocumentVectorIndexingService, QdrantDocumentVectorIndexingService>();
            services.AddScoped<IDocumentVectorSearchService, QdrantDocumentVectorSearchService>();
        }
        else
        {
            services.AddScoped<IDocumentVectorIndexingService, DisabledDocumentVectorIndexingService>();
            services.AddScoped<IDocumentVectorSearchService, DisabledDocumentVectorSearchService>();
        }

        var storageOptions = configuration.GetSection(DocumentFileStorageOptions.SectionName).Get<DocumentFileStorageOptions>()
            ?? new DocumentFileStorageOptions();
        if (storageOptions.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<LocalDocumentFileStorage>();
            services.AddScoped<MinioDocumentFileStorage>();
            services.AddScoped<IDocumentFileStorage>(sp => sp.GetRequiredService<MinioDocumentFileStorage>());
        }
        else
        {
            services.AddScoped<IDocumentFileStorage, LocalDocumentFileStorage>();
        }

        services.AddScoped<ICadParsingPlaceholder, DisabledCadParsingPlaceholder>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services;
    }
}
