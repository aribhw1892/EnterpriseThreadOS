using System.Net;
using System.Net.Http.Json;
using System.Text;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class ImportMappingLearningSignalTests
{
    [Fact]
    public async Task MappingApproveRejectAndCorrectedMappingsEmitLearningSignals()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var batch = await CreateImportBatchAsync(client, context.TenantId, context.UserId);
        await UploadCsvAsync(client, context.TenantId, context.UserId, batch.Id, "partNumber,lifecycle,cost\nP-100,released,12.50\n");

        using var previewRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batch.Id}/mapping-preview")
        {
            Content = JsonContent.Create(new ImportPreviewRequest(null, 25))
        };
        AddTenantHeaders(previewRequest, context.TenantId, context.UserId);
        var previewResponse = await client.SendAsync(previewRequest);
        Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());

        var mapping = await CreateMappingAsync(client, context.TenantId, context.UserId, batch.Id, ["released"]);
        await RejectMappingAsync(client, context.TenantId, context.UserId, mapping.Id, "Needs review.");
        var corrected = await CreateMappingAsync(client, context.TenantId, context.UserId, batch.Id, ["released"], "1.0.1");
        await ApproveMappingAsync(client, context.TenantId, context.UserId, corrected.Id);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var signals = await dbContext.ImportMappingLearningSignalInputs
            .Where(item => item.TenantId == context.TenantId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();

        Assert.Contains(signals, item => item.EventType == ImportMappingLearningSignalEventType.Corrected);
        Assert.Contains(signals, item => item.EventType == ImportMappingLearningSignalEventType.Rejected);
        Assert.Contains(signals, item => item.EventType == ImportMappingLearningSignalEventType.Approved);
        Assert.All(signals, item => Assert.False(item.AutonomousRetraining));
    }

    private static WebApplicationFactory<Program> CreateApplication()
    {
        var databaseName = Guid.NewGuid().ToString();
        var storageRoot = Path.Combine(Path.GetTempPath(), "etos-import-learning-tests", Guid.NewGuid().ToString("N"));
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OperationalStore:ConnectionString"] = "Host=localhost;Database=test;Username=test;Password=test",
                        ["ImportFileStorage:RootPath"] = storageRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options => options.UseInMemoryDatabase(databaseName));
                });
            });
    }

    private static async Task<ImportBatchResponse> CreateImportBatchAsync(HttpClient client, Guid tenantId, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/batches")
        {
            Content = JsonContent.Create(new CreateImportBatchRequest("demo-pdm", "Demo import batch.", null))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var batch = await response.Content.ReadFromJsonAsync<ImportBatchResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(batch);
        return batch;
    }

    private static async Task UploadCsvAsync(HttpClient client, Guid tenantId, Guid userId, Guid batchId, string csv)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/files");
        AddTenantHeaders(request, tenantId, userId);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");
        request.Content = content;
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<ImportMappingVersionResponse> CreateMappingAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid batchId,
        IReadOnlyCollection<string> lifecycleValues,
        string versionLabel = "1.0.0")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batchId,
                versionLabel,
                "Test mapping.",
                [
                    new CreateImportColumnMappingRequest("partNumber", "part", "partNumber", true, true),
                    new CreateImportColumnMappingRequest("cost", "part", "cost", false, false)
                ],
                lifecycleValues.Select(value => new CreateImportLifecycleMappingRequest(value, "released")).ToList()))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var mapping = await response.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.NotNull(mapping);
        return mapping;
    }

    private static async Task ApproveMappingAsync(HttpClient client, Guid tenantId, Guid userId, Guid mappingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mappingId}/approve")
        {
            Content = JsonContent.Create(new ApproveImportMappingRequest("Approved by test."))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task RejectMappingAsync(HttpClient client, Guid tenantId, Guid userId, Guid mappingId, string reason)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mappingId}/reject")
        {
            Content = JsonContent.Create(new RejectImportMappingRequest("Rejected by test.", reason))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }
}
