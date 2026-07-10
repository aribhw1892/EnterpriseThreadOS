using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests.Fixtures;

public static class ImportFlowTestSupport
{
    internal sealed record ImportFlowContext(Guid TenantId, Guid UserId);

    internal static WebApplicationFactory<Program> CreateApplication(RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var storageRoot = Path.Combine(Path.GetTempPath(), "etos-import-flow-tests", Guid.NewGuid().ToString("N"));
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ImportFileStorage:RootPath"] = storageRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false",
                        ["ImportMappingSuggestions:DefaultProviderKey"] = "rule-based-v1",
                        ["ReferencePackages:RootPath"] = packagesRoot
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    services.RemoveAll<IGraphMemoryService>();
                    services.AddSingleton<IGraphMemoryService>(graphMemory ?? new RecordingGraphMemoryService());
                });
            });
    }

    internal static async Task<ImportFlowContext> CreatePublishedModelContextAsync(
        HttpClient client,
        string tenantIdentifier = "tenant-a",
        string email = "admin@example.test")
    {
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, tenantIdentifier, email);
        return new ImportFlowContext(packageContext.TenantId, packageContext.UserId);
    }

    internal static string GetDemoCsvPath(string fileName)
        => Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages")),
            "manufacturing-reference",
            "demo-imports",
            fileName);

    internal static async Task<string> ReadDemoCsvAsync(string fileName)
        => await File.ReadAllTextAsync(GetDemoCsvPath(fileName));

    internal static async Task<ImportBatchResponse> CreateImportBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        string sourceSystem,
        string? description = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/batches")
        {
            Content = JsonContent.Create(new CreateImportBatchRequest(
                sourceSystem,
                description ?? $"{sourceSystem} import batch.",
                null))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var batch = await response.Content.ReadFromJsonAsync<ImportBatchResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(batch);
        return batch;
    }

    internal static async Task<UploadImportFileResponse> UploadCsvAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId,
        string csv,
        string fileName = "import.csv")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/files");
        AddTenantHeaders(request, context.TenantId, context.UserId);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        request.Content = content;

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var upload = System.Text.Json.JsonSerializer.Deserialize<UploadImportFileResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(upload);
        return upload;
    }

    internal static async Task<ImportMappingVersionResponse> CreateMappingFromPreviewAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId,
        string versionLabel = "1.0.0")
    {
        using var previewRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/mapping-preview")
        {
            Content = JsonContent.Create(new ImportPreviewRequest(null, 10, null, false, null, null))
        };
        AddTenantHeaders(previewRequest, context.TenantId, context.UserId);
        var previewResponse = await client.SendAsync(previewRequest);
        var previewBody = await previewResponse.Content.ReadAsStringAsync();
        Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, previewBody);
        var preview = System.Text.Json.JsonSerializer.Deserialize<ImportPreviewResponse>(
            previewBody,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(preview);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batchId,
                versionLabel,
                "Preview-aligned mapping.",
                preview.ColumnSuggestions
                    .Where(item => item.CanonicalAttributeKey is not null || item.IsIdentityField)
                    .Select(item => new CreateImportColumnMappingRequest(
                        item.SourceColumn,
                        item.CanonicalObjectType,
                        item.CanonicalAttributeKey,
                        item.IsIdentityField,
                        item.IsRequired))
                    .ToList(),
                preview.LifecycleSuggestions
                    .Select(item => new CreateImportLifecycleMappingRequest(
                        item.SourceValue,
                        item.CanonicalLifecycleKey))
                    .ToList()))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var mapping = System.Text.Json.JsonSerializer.Deserialize<ImportMappingVersionResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(mapping);
        return mapping;
    }

    internal static async Task<ImportMappingVersionResponse> CreateMappingAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId,
        IReadOnlyCollection<string> lifecycleValues)
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
                lifecycleValues.Select(value => new CreateImportLifecycleMappingRequest(value, "released")).ToList()))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var mapping = System.Text.Json.JsonSerializer.Deserialize<ImportMappingVersionResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(mapping);
        return mapping;
    }

    internal static async Task<ImportMappingVersionResponse> ApproveMappingAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid mappingId,
        string? structuralRelationshipType = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mappingId}/approve")
        {
            Content = JsonContent.Create(new ApproveImportMappingRequest("Approved by test.", structuralRelationshipType))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var mapping = await response.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(mapping);
        return mapping;
    }

    internal static async Task<ImportMappingVersionResponse> CreateStructuralMappingAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        string? structuralRelationshipType = null,
        string versionLabel = "1.0.0",
        IReadOnlyCollection<CreateImportLifecycleMappingRequest>? lifecycleMappings = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batchId,
                versionLabel,
                "Structural test mapping.",
                columnMappings,
                lifecycleMappings ?? [],
                structuralRelationshipType))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var mapping = System.Text.Json.JsonSerializer.Deserialize<ImportMappingVersionResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(mapping);
        return mapping;
    }

    internal static async Task<ImportValidationResponse> ValidateBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/validate")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var validation = System.Text.Json.JsonSerializer.Deserialize<ImportValidationResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(validation);
        return validation;
    }

    internal static async Task<ImportStagingGraphRunResponse> StageBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/stage")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var run = System.Text.Json.JsonSerializer.Deserialize<ImportStagingGraphRunResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(run);
        return run;
    }

    internal static async Task<ImportPromotionRunResponse> PromoteBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/promote")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var run = System.Text.Json.JsonSerializer.Deserialize<ImportPromotionRunResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(run);
        return run;
    }

    internal static async Task<RejectedStagingSummaryResponse> RejectStagingAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/reject-staging")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var summary = System.Text.Json.JsonSerializer.Deserialize<RejectedStagingSummaryResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(summary);
        return summary;
    }

    internal static async Task<BomComparisonRunResponse> CreateBomComparisonAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/bom-comparison")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var comparison = System.Text.Json.JsonSerializer.Deserialize<BomComparisonRunResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(comparison);
        return comparison;
    }

    internal static async Task<GraphSnapshotContract> CaptureSnapshotAsync(
        HttpClient client,
        ImportFlowContext context,
        GraphSpace graphSpace)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/graph/snapshots")
        {
            Content = JsonContent.Create(new CaptureGraphSnapshotRequest(graphSpace))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<GraphSnapshotContract>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(snapshot);
        return snapshot;
    }

    internal static async Task<GraphDiffContract> CreateGraphDiffAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid fromSnapshotId,
        Guid toSnapshotId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/graph/diffs")
        {
            Content = JsonContent.Create(new CreateGraphDiffRequest(fromSnapshotId, toSnapshotId))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var diff = System.Text.Json.JsonSerializer.Deserialize<GraphDiffContract>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(diff);
        return diff;
    }

    internal static async Task<IdentityCandidateGenerationResponse> GenerateIdentityCandidatesAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/identity-resolution/batches/{batchId}/candidates/generate")
        {
            Content = JsonContent.Create(new GenerateIdentityCandidatesRequest(null))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var generated = await response.Content.ReadFromJsonAsync<IdentityCandidateGenerationResponse>();
        Assert.NotNull(generated);
        return generated;
    }

    internal static async Task<IdentityCandidateLinkResponse> ApproveIdentityCandidateAsync(
        HttpClient client,
        ImportFlowContext context,
        Guid candidateId,
        string rationale = "Approved for MVP demonstration flow.")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/identity-resolution/candidates/{candidateId}/approve")
        {
            Content = JsonContent.Create(new IdentityReviewDecisionRequest(rationale))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var candidate = await response.Content.ReadFromJsonAsync<IdentityCandidateLinkResponse>();
        Assert.NotNull(candidate);
        return candidate;
    }

    internal static async Task<(ImportBatchResponse Batch, ImportMappingVersionResponse Mapping)> PrepareStagedImportAsync(
        HttpClient client,
        ImportFlowContext context,
        string sourceSystem,
        string csvFileName,
        IReadOnlyCollection<string>? lifecycleValues = null)
    {
        var csv = await ReadDemoCsvAsync(csvFileName);
        var batch = await CreateImportBatchAsync(client, context, sourceSystem);
        await UploadCsvAsync(client, context, batch.Id, csv, csvFileName);
        var mapping = lifecycleValues is null
            ? await CreateMappingFromPreviewAsync(client, context, batch.Id, $"demo-{Guid.NewGuid():N}"[..12])
            : await CreateMappingAsync(client, context, batch.Id, lifecycleValues);
        await ApproveMappingAsync(client, context, mapping.Id);
        await ValidateBatchAsync(client, context, batch.Id);
        await StageBatchAsync(client, context, batch.Id);
        return (batch, mapping);
    }

    internal static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }

    public sealed class RecordingGraphMemoryService : IGraphMemoryService
    {
        public List<CreateGraphNodeRequest> CreatedNodeRequests { get; } = [];
        public List<CreateGraphRelationshipRequest> CreatedRelationshipRequests { get; } = [];
        public List<BaseNode> Nodes { get; } = [];
        public List<BaseRelationship> Relationships { get; } = [];

        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            CreatedNodeRequests.Add(request);
            var now = DateTimeOffset.UtcNow;
            var node = new BaseNode(
                Guid.NewGuid(),
                request.TenantId,
                request.GraphSpace,
                request.ObjectType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now);
            Nodes.Add(node);
            return Task.FromResult(node);
        }

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Nodes.SingleOrDefault(node => node.TenantId == tenantId && node.NodeId == nodeId));
        }

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BaseRelationship> CreateRelationshipAsync(
            CreateGraphRelationshipRequest request,
            CancellationToken cancellationToken)
        {
            CreatedRelationshipRequests.Add(request);
            var now = DateTimeOffset.UtcNow;
            var relationship = new BaseRelationship(
                Guid.NewGuid(),
                request.TenantId,
                request.FromNodeId,
                request.ToNodeId,
                request.RelationshipType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now);
            Relationships.Add(relationship);
            return Task.FromResult(relationship);
        }

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var startNode = Nodes.FirstOrDefault(node => node.TenantId == request.TenantId && node.NodeId == request.StartNodeId)
                ?? new BaseNode(
                    request.StartNodeId,
                    request.TenantId,
                    request.GraphSpace ?? GraphSpace.Trusted,
                    "part",
                    TrustState.Trusted,
                    new Dictionary<string, string?>(),
                    null,
                    now,
                    now);
            return Task.FromResult(new GraphTraversalResult(startNode, [startNode], []));
        }

        public Task<GraphReadModel> ListGraphAsync(
            Guid tenantId,
            GraphSpace? graphSpace,
            string? sourceBatchId,
            IReadOnlyCollection<Guid>? nodeIds,
            IReadOnlyCollection<Guid>? relationshipIds,
            CancellationToken cancellationToken)
        {
            var nodes = Nodes
                .Where(node => node.TenantId == tenantId
                    && (graphSpace is null || node.GraphSpace == graphSpace)
                    && (sourceBatchId is null || node.SourceReference?.SourceBatchId == sourceBatchId)
                    && (nodeIds is null || nodeIds.Count == 0 || nodeIds.Contains(node.NodeId)))
                .ToList();
            var relationships = Relationships
                .Where(relationship => relationship.TenantId == tenantId
                    && (sourceBatchId is null || relationship.SourceReference?.SourceBatchId == sourceBatchId)
                    && (relationshipIds is null || relationshipIds.Count == 0 || relationshipIds.Contains(relationship.RelationshipId)))
                .ToList();
            return Task.FromResult(new GraphReadModel(nodes, relationships));
        }

        public async Task<GraphPromotionCopyResult> PromoteStagingAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> stagingNodeIds,
            IReadOnlyCollection<Guid> stagingRelationshipIds,
            CancellationToken cancellationToken)
        {
            var staging = await ListGraphAsync(tenantId, GraphSpace.Staging, null, stagingNodeIds, stagingRelationshipIds, cancellationToken);
            var nodeMap = new Dictionary<Guid, Guid>();
            foreach (var node in staging.Nodes)
            {
                var promoted = await CreateNodeAsync(
                    new CreateGraphNodeRequest(tenantId, GraphSpace.Trusted, node.ObjectType, TrustState.Trusted, node.Attributes, node.SourceReference),
                    cancellationToken);
                nodeMap[node.NodeId] = promoted.NodeId;
            }

            var promotedRelationshipIds = new List<Guid>();
            foreach (var relationship in staging.Relationships)
            {
                if (!nodeMap.TryGetValue(relationship.FromNodeId, out var fromNodeId)
                    || !nodeMap.TryGetValue(relationship.ToNodeId, out var toNodeId))
                {
                    continue;
                }

                var promoted = await CreateRelationshipAsync(
                    new CreateGraphRelationshipRequest(
                        tenantId,
                        fromNodeId,
                        toNodeId,
                        relationship.RelationshipType,
                        TrustState.Trusted,
                        relationship.Attributes,
                        relationship.SourceReference),
                    cancellationToken);
                promotedRelationshipIds.Add(promoted.RelationshipId);
            }

            return new GraphPromotionCopyResult(nodeMap.Values.ToList(), promotedRelationshipIds);
        }
    }
}
