using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.Packages;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class ManufacturingReferencePackageTests
{
    [Fact]
    public async Task Install_PublishesModelPackageWithImportAndQueryProfiles()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var package = await dbContext.ModelPackageVersions.SingleAsync(item => item.Id == context.ModelPackage.Id);

        Assert.Equal(ManufacturingReferencePackageKeys.PackageKey, package.Key);
        Assert.False(string.IsNullOrWhiteSpace(package.ImportProfileJson));
        Assert.False(string.IsNullOrWhiteSpace(package.QueryIntentExtensionsJson));
        Assert.Contains("bom-impact-context", package.QueryIntentExtensionsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reinstall_IsIdempotentForSameTenant()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var first = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-ref-a", "owner@example.test");
        var second = await InstallReferencePackageAsync(client, first.TenantId, first.UserId);

        Assert.True(second.AlreadyInstalled);
        Assert.Equal(first.ModelPackage.Id, second.ModelPackage.Id);
    }

    [Fact]
    public async Task Install_PublishesCapabilityPolicyOptimizationAndAgentTemplateChain()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-ref-b", "owner2@example.test");

        using var capabilityRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/capabilities");
        AddTenantHeaders(capabilityRequest, context.TenantId, context.UserId);
        var capabilityResponse = await client.SendAsync(capabilityRequest);
        var capabilities = await capabilityResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<CapabilityDefinitionArtifactSummaryResponse>>();
        Assert.True(capabilityResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(capabilities);
        Assert.Contains(capabilities, item => item.CapabilityKey == "bom-impact-analysis");

        using var policyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/business-policies");
        AddTenantHeaders(policyRequest, context.TenantId, context.UserId);
        var policyResponse = await client.SendAsync(policyRequest);
        var policies = await policyResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<BusinessPolicyDefinitionArtifactSummaryResponse>>();
        Assert.True(policyResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(policies);
        Assert.Contains(policies, item => item.PolicyKey == "min-maturity-85");

        using var optimizationRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/optimization-models");
        AddTenantHeaders(optimizationRequest, context.TenantId, context.UserId);
        var optimizationResponse = await client.SendAsync(optimizationRequest);
        var optimizations = await optimizationResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<OptimizationModelDefinitionArtifactSummaryResponse>>();
        Assert.True(optimizationResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(optimizations);
        Assert.Contains(optimizations, item => item.OptimizationKey == "minimize-transport-distance");

        using var templateRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/agent-templates");
        AddTenantHeaders(templateRequest, context.TenantId, context.UserId);
        var templateResponse = await client.SendAsync(templateRequest);
        var templates = await templateResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AgentTemplateDefinitionArtifactSummaryResponse>>();
        Assert.True(templateResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(templates);
        Assert.Contains(templates, item => item.TemplateKey == "manufacturing-investigator");
    }

    [Fact]
    public async Task BomComparison_ThroughInstalledPackage_ReportsMismatches()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));
        var csv = await File.ReadAllTextAsync(Path.Combine(packagesRoot, "manufacturing-reference", "demo-imports", "bom-comparison.csv"));

        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, csv);
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);

        var staging = await StageBatchAsync(client, context, batch.Id);
        var comparison = await CreateBomComparisonAsync(client, context, batch.Id);

        Assert.Equal(4, staging.RelationshipCount);
        Assert.Equal(1, comparison.MissingInPrimarySideCount);
        Assert.Equal(1, comparison.MissingInSecondarySideCount);
        Assert.Equal(1, comparison.QuantityMismatchCount);
        Assert.Equal(1, comparison.UsageReferenceMismatchCount);
    }

    [Fact]
    public async Task CrossTenantInstall_IsDenied()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var owner = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-ref-c", "owner3@example.test");

        var otherUserId = Guid.NewGuid();
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-ref-d");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/development/install-reference-package")
        {
            Content = JsonContent.Create(new InstallReferencePackageRequest(ManufacturingReferencePackageKeys.PackageKey))
        };
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, owner.TenantId.ToString());

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateApplication(RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var storageRoot = Path.Combine(Path.GetTempPath(), "etos-ref-package-tests", Guid.NewGuid().ToString("N"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ImportFileStorage:RootPath"] = storageRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false",
                        ["ReferencePackages:RootPath"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"))
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    if (graphMemory is not null)
                    {
                        services.RemoveAll<IGraphMemoryService>();
                        services.AddSingleton<IGraphMemoryService>(graphMemory);
                    }
                });
            });
    }

    private static async Task<InstallReferencePackageResponse> InstallReferencePackageAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/development/install-reference-package")
        {
            Content = JsonContent.Create(new InstallReferencePackageRequest(ManufacturingReferencePackageKeys.PackageKey))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var installed = await response.Content.ReadFromJsonAsync<InstallReferencePackageResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(installed);
        return installed;
    }

    private static async Task CreateUserAsync(HttpClient client, Guid actorUserId, Guid userId, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/users")
        {
            Content = JsonContent.Create(new CreateUserRequest(userId, email, email, email, "local-password"))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<TenantResponse> CreateTenantAsync(HttpClient client, Guid actorUserId, string identifier)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/tenants")
        {
            Content = JsonContent.Create(new CreateTenantRequest(identifier, identifier, null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());
        var response = await client.SendAsync(request);
        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(tenant);
        return tenant;
    }

    private static async Task<ImportBatchResponse> CreateImportBatchAsync(HttpClient client, ManufacturingModelPackageFixture.PublishedPackageContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/batches")
        {
            Content = JsonContent.Create(new CreateImportBatchRequest("demo-pdm", "Demo import batch.", null))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var batch = await response.Content.ReadFromJsonAsync<ImportBatchResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(batch);
        return batch;
    }

    private static async Task UploadCsvAsync(HttpClient client, ManufacturingModelPackageFixture.PublishedPackageContext context, Guid batchId, string csv)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/files");
        AddTenantHeaders(request, context.TenantId, context.UserId);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");
        request.Content = content;
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<ImportMappingVersionResponse> CreateMappingAsync(
        HttpClient client,
        ManufacturingModelPackageFixture.PublishedPackageContext context,
        Guid batchId,
        IReadOnlyCollection<string> lifecycleKeys)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batchId,
                "1.0.0",
                "Test mapping.",
                [
                    new CreateImportColumnMappingRequest("partNumber", "part", "partNumber", true, true),
                    new CreateImportColumnMappingRequest("cost", "part", "cost", false, false)
                ],
                lifecycleKeys.Select(key => new CreateImportLifecycleMappingRequest(key, "released")).ToList()))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var mapping = await response.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(mapping);
        return mapping;
    }

    private static async Task ApproveMappingAsync(HttpClient client, ManufacturingModelPackageFixture.PublishedPackageContext context, Guid mappingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mappingId}/approve")
        {
            Content = JsonContent.Create(new ApproveImportMappingRequest("Approved by reference package test."))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<ImportStagingGraphRunResponse> StageBatchAsync(
        HttpClient client,
        ManufacturingModelPackageFixture.PublishedPackageContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/stage")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var staging = await response.Content.ReadFromJsonAsync<ImportStagingGraphRunResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(staging);
        return staging;
    }

    private static async Task<BomComparisonRunResponse> CreateBomComparisonAsync(
        HttpClient client,
        ManufacturingModelPackageFixture.PublishedPackageContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/bom-comparison")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var comparison = await response.Content.ReadFromJsonAsync<BomComparisonRunResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(comparison);
        return comparison;
    }

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }

    private sealed class RecordingGraphMemoryService : IGraphMemoryService
    {
        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new BaseNode(
                Guid.NewGuid(),
                request.TenantId,
                request.GraphSpace,
                request.ObjectType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now));
        }

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken) =>
            Task.FromResult<BaseNode?>(null);

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new BaseRelationship(
                Guid.NewGuid(),
                request.TenantId,
                request.FromNodeId,
                request.ToNodeId,
                request.RelationshipType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now));
        }

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GraphReadModel> ListGraphAsync(
            Guid tenantId,
            GraphSpace? graphSpace,
            string? sourceBatchId,
            IReadOnlyCollection<Guid>? nodeIds,
            IReadOnlyCollection<Guid>? relationshipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphReadModel([], []));

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> nodeIds,
            IReadOnlyCollection<Guid> relationshipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphPromotionCopyResult([], []));
    }
}
