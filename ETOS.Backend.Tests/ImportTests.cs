using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class ImportTests
{
    [Fact]
    public async Task RawFileEvidenceIsStoredWithChecksumAndAuditLink()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        var csv = "partNumber,lifecycle,cost\nP-100,released,12.50\n";

        var upload = await UploadCsvAsync(client, context, batch.Id, csv);

        Assert.Equal(batch.Id, upload.Evidence.ImportBatchId);
        Assert.Equal(Sha256(csv), upload.Evidence.Sha256Checksum);
        Assert.DoesNotContain(csv, upload.Evidence.StorageKey, StringComparison.OrdinalIgnoreCase);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Id == upload.Evidence.AuditRecordId);
        Assert.Equal("imports.files.upload", audit.Action);
        Assert.DoesNotContain(csv, audit.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MappingApprovalIsRequiredAndApprovedMappingsHaveNoUpdateEndpoint()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, lifecycleValues: ["released"]);

        using var blockedStageRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batch.Id}/stage")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(blockedStageRequest, context.TenantId, context.UserId);
        var blockedStageResponse = await client.SendAsync(blockedStageRequest);
        var blockedProblem = await blockedStageResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, blockedStageResponse.StatusCode);
        Assert.NotNull(blockedProblem);
        Assert.Contains("approved import mapping", blockedProblem.Error, StringComparison.OrdinalIgnoreCase);

        var approved = await ApproveMappingAsync(client, context, mapping.Id);
        Assert.Equal(ImportMappingState.Approved, approved.State);
        Assert.NotNull(approved.ApprovedAt);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/imports/mappings/{mapping.Id}")
        {
            Content = JsonContent.Create(new { summary = "mutated" })
        };
        AddTenantHeaders(updateRequest, context.TenantId, context.UserId);
        var updateResponse = await client.SendAsync(updateRequest);

        Assert.True(
            updateResponse.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            await updateResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ValidationPersistsRequiredFieldTypeAndLifecycleFailures()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\n,unknown,not-a-number\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, lifecycleValues: ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);

        var validation = await ValidateBatchAsync(client, context, batch.Id);

        Assert.False(validation.IsValid);
        Assert.Equal(3, validation.ErrorCount);
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "missing_required_value");
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "invalid_value_type");
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "invalid_lifecycle_value");
    }

    [Fact]
    public async Task StagingCreatesUnverifiedStagingGraphNodesWithSourceReferences()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\nP-100,released,12.50\nP-200,released,15.25\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, lifecycleValues: ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);

        var run = await StageBatchAsync(client, context, batch.Id);

        Assert.Equal(ImportStagingRunStatus.Completed, run.Status);
        Assert.Equal(2, run.NodeCount);
        Assert.All(graphMemory.CreatedNodeRequests, request =>
        {
            Assert.Equal(GraphSpace.Staging, request.GraphSpace);
            Assert.Equal(TrustState.Unverified, request.TrustState);
            Assert.Equal("demo-pdm", request.SourceReference?.SourceSystem);
            Assert.Equal(batch.Id.ToString(), request.SourceReference?.SourceBatchId);
        });
    }

    [Fact]
    public async Task CrossTenantImportAccessIsDeniedAndAudited()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client, "tenant-a", "admin-a@example.test");
        var otherContext = await CreatePublishedModelContextAsync(client, "tenant-b", "admin-b@example.test");
        var batch = await CreateImportBatchAsync(client, context);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/imports/batches/{batch.Id}");
        AddTenantHeaders(request, otherContext.TenantId, otherContext.UserId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var denial = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "imports.batches.get" && record.Result == AuditResult.Denied);

        Assert.Equal(otherContext.TenantId, denial.TenantId);
        Assert.Equal("import_tenant_mismatch", denial.Reason);
    }

    [Fact]
    public async Task PromotionIsBlockedByValidationErrors()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);
        await StageBatchAsync(client, context, batch.Id);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
            dbContext.ImportValidationIssues.Add(new ImportValidationIssue
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                ImportBatchId = batch.Id,
                ImportMappingVersionId = mapping.Id,
                Severity = ImportIssueSeverity.Error,
                IssueCode = "blocking_issue",
                Message = "Blocking issue.",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batch.Id}/promote")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(graphMemory.Nodes, node => node.GraphSpace == GraphSpace.Trusted);
    }

    [Fact]
    public async Task PromotionCopiesStagingGraphToTrustedAndRecordsAudit()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\nP-100,released,12.50\nP-200,released,15.25\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);
        await StageBatchAsync(client, context, batch.Id);

        var promotion = await PromoteBatchAsync(client, context, batch.Id);

        Assert.Equal(ImportPromotionRunStatus.Completed, promotion.Status);
        Assert.Equal(2, promotion.PromotedNodeCount);
        Assert.NotNull(promotion.AuditRecordId);
        Assert.Equal(2, graphMemory.Nodes.Count(node => node.GraphSpace == GraphSpace.Trusted && node.TrustState == TrustState.Trusted));
    }

    [Fact]
    public async Task RejectedStagingStoresSummariesWithoutRawPayload()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        const string csv = "partNumber,lifecycle,cost\nP-100,released,12.50\n";
        await UploadCsvAsync(client, context, batch.Id, csv);
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);
        await StageBatchAsync(client, context, batch.Id);

        var rejected = await RejectStagingAsync(client, context, batch.Id);

        Assert.Equal(1, rejected.NodeCount);
        Assert.DoesNotContain(csv, rejected.ValidationSummaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(csv, rejected.DecisionSummaryJson, StringComparison.OrdinalIgnoreCase);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var updatedBatch = await dbContext.ImportBatches.SingleAsync(item => item.Id == batch.Id);
        Assert.Equal(ImportBatchStatus.Rejected, updatedBatch.Status);
    }

    [Fact]
    public async Task BomMetadataStagesRelationshipsAndComparisonReportsMismatches()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        var csv = "bomSide,partNumber,lifecycle,cost,parent,child,quantity,unit,usage\nCAD,A,released,1,A,B,2,ea,R1\nEBOM,A,released,1,A,B,3,ea,R2\nCAD,A,released,1,A,C,1,ea,R3\nEBOM,A,released,1,A,D,1,ea,R4\n";
        await UploadCsvAsync(client, context, batch.Id, csv);
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);

        var staging = await StageBatchAsync(client, context, batch.Id);
        var comparison = await CreateBomComparisonAsync(client, context, batch.Id);

        Assert.Equal(4, staging.RelationshipCount);
        Assert.Equal(1, comparison.MissingInCadCount);
        Assert.Equal(1, comparison.MissingInEbomCount);
        Assert.Equal(1, comparison.QuantityMismatchCount);
        Assert.Equal(1, comparison.UsageReferenceMismatchCount);
    }

    [Fact]
    public async Task SnapshotsPersistDeterministicPayloadsAndDiffsReportChanges()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);
        await StageBatchAsync(client, context, batch.Id);
        await PromoteBatchAsync(client, context, batch.Id);

        var first = await CaptureSnapshotAsync(client, context, GraphSpace.Trusted);
        await graphMemory.CreateNodeAsync(
            new CreateGraphNodeRequest(
                context.TenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["partNumber"] = "P-200" },
                new GraphSourceReference("demo-pdm", "P-200", batch.Id.ToString())),
            CancellationToken.None);
        var second = await CaptureSnapshotAsync(client, context, GraphSpace.Trusted);
        var diff = await CreateGraphDiffAsync(client, context, first.SnapshotId, second.SnapshotId);

        Assert.NotEqual(first.ChecksumSha256, second.ChecksumSha256);
        Assert.Contains("node addition", diff.SafeSummary, StringComparison.OrdinalIgnoreCase);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        Assert.Equal(2, await dbContext.GraphSnapshots.CountAsync());
        Assert.Equal(1, await dbContext.GraphDiffs.CountAsync());
    }

    private static WebApplicationFactory<Program> CreateApplication(RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var storageRoot = Path.Combine(Path.GetTempPath(), "etos-import-tests", Guid.NewGuid().ToString("N"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ImportFileStorage:RootPath"] = storageRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false"
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

    private static async Task<TestContext> CreatePublishedModelContextAsync(
        HttpClient client,
        string tenantIdentifier = "tenant-a",
        string email = "admin@example.test")
    {
        var userId = Guid.NewGuid();
        await CreateUserAsync(client, userId, userId, email);
        var tenant = await CreateTenantAsync(client, userId, tenantIdentifier);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ontology = await CreateOntologyAsync(client, tenant.Id, userId, suffix);
        var semanticLayer = await CreateSemanticLayerAsync(client, tenant.Id, userId, ontology.Id, suffix);
        var lifecycle = await CreateLifecycleAsync(client, tenant.Id, userId, suffix);
        var attributeSchema = await CreateAttributeSchemaAsync(client, tenant.Id, userId, ontology.Id, suffix);

        await PublishAsync<OntologyVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/versions/{ontology.Id}/publish");
        await PublishAsync<SemanticLayerVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/semantic-layers/{semanticLayer.Id}/publish");
        await PublishAsync<LifecycleVocabularyVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/lifecycle-vocabularies/{lifecycle.Id}/publish");
        await PublishAsync<AttributeSchemaVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/attribute-schemas/{attributeSchema.Id}/publish");
        var modelPackage = await CreateModelPackageAsync(client, tenant.Id, userId, ontology.Id, semanticLayer.Id, lifecycle.Id, attributeSchema.Id, suffix);
        await PublishAsync<ModelPackageVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/model-packages/{modelPackage.Id}/publish");

        return new TestContext(tenant.Id, userId);
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

    private static async Task<OntologyVersionResponse> CreateOntologyAsync(HttpClient client, Guid tenantId, Guid userId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/versions")
        {
            Content = JsonContent.Create(new CreateOntologyVersionRequest(
                $"canonical-manufacturing-{suffix}",
                "1.0.0",
                "Canonical manufacturing ontology.",
                [new CreateObjectTypeDefinitionRequest("part", "Part", "Source-owned part.", """["partNumber"]""", "Part identity.")],
                [],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var ontology = await response.Content.ReadFromJsonAsync<OntologyVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ontology);
        return ontology;
    }

    private static async Task<SemanticLayerVersionResponse> CreateSemanticLayerAsync(HttpClient client, Guid tenantId, Guid userId, Guid ontologyVersionId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/semantic-layers")
        {
            Content = JsonContent.Create(new CreateSemanticLayerVersionRequest(
                $"canonical-semantic-{suffix}",
                "1.0.0",
                "Canonical graph mappings.",
                ontologyVersionId,
                """{"part":"Part"}""",
                """{}"""))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var semanticLayer = await response.Content.ReadFromJsonAsync<SemanticLayerVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(semanticLayer);
        return semanticLayer;
    }

    private static async Task<LifecycleVocabularyVersionResponse> CreateLifecycleAsync(HttpClient client, Guid tenantId, Guid userId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/lifecycle-vocabularies")
        {
            Content = JsonContent.Create(new CreateLifecycleVocabularyVersionRequest(
                $"canonical-lifecycle-{suffix}",
                "1.0.0",
                "Canonical lifecycle.",
                [
                    new CreateLifecycleStateDefinitionRequest("draft", "Draft", "working", 10, false),
                    new CreateLifecycleStateDefinitionRequest("released", "Released", "released", 20, false)
                ],
                [new CreateLifecycleTransitionDefinitionRequest("draft", "released", true, "Release approval.")]))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var lifecycle = await response.Content.ReadFromJsonAsync<LifecycleVocabularyVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(lifecycle);
        return lifecycle;
    }

    private static async Task<AttributeSchemaVersionResponse> CreateAttributeSchemaAsync(HttpClient client, Guid tenantId, Guid userId, Guid ontologyVersionId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/attribute-schemas")
        {
            Content = JsonContent.Create(new CreateAttributeSchemaVersionRequest(
                $"canonical-attributes-{suffix}",
                "1.0.0",
                "Canonical attributes.",
                ontologyVersionId,
                [
                    new CreateAttributeDefinitionRequest("partNumber", "part", AttributeValueType.Text, true, """{"maxLength":80}""", AttributeVisibility.Internal, null, true, true, "internal", "Part Number", "Part number identity."),
                    new CreateAttributeDefinitionRequest("cost", "part", AttributeValueType.Number, false, """{"minimum":0}""", AttributeVisibility.Internal, null, false, false, "internal", "Cost", "Part cost.")
                ]))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var attributeSchema = await response.Content.ReadFromJsonAsync<AttributeSchemaVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(attributeSchema);
        return attributeSchema;
    }

    private static async Task<ModelPackageVersionResponse> CreateModelPackageAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid ontologyVersionId,
        Guid semanticLayerVersionId,
        Guid lifecycleVocabularyVersionId,
        Guid attributeSchemaVersionId,
        string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/model-packages")
        {
            Content = JsonContent.Create(new CreateModelPackageVersionRequest(
                $"canonical-package-{suffix}",
                "Canonical Package",
                "1.0.0",
                "Canonical model package.",
                ontologyVersionId,
                semanticLayerVersionId,
                lifecycleVocabularyVersionId,
                attributeSchemaVersionId))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var package = await response.Content.ReadFromJsonAsync<ModelPackageVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(package);
        return package;
    }

    private static async Task<TResponse> PublishAsync<TResponse>(HttpClient client, Guid tenantId, Guid userId, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new PublishOntologyVersionRequest("Published by test."))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var published = await response.Content.ReadFromJsonAsync<TResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(published);
        return published;
    }

    private static async Task<ImportBatchResponse> CreateImportBatchAsync(HttpClient client, TestContext context)
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

    private static async Task<UploadImportFileResponse> UploadCsvAsync(HttpClient client, TestContext context, Guid batchId, string csv)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/files");
        AddTenantHeaders(request, context.TenantId, context.UserId);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");
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

    private static async Task<ImportMappingVersionResponse> CreateMappingAsync(
        HttpClient client,
        TestContext context,
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

    private static async Task<ImportMappingVersionResponse> ApproveMappingAsync(HttpClient client, TestContext context, Guid mappingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mappingId}/approve")
        {
            Content = JsonContent.Create(new ApproveImportMappingRequest("Approved by test."))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var mapping = await response.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(mapping);
        return mapping;
    }

    private static async Task<ImportValidationResponse> ValidateBatchAsync(HttpClient client, TestContext context, Guid batchId)
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

    private static async Task<ImportStagingGraphRunResponse> StageBatchAsync(HttpClient client, TestContext context, Guid batchId)
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

    private static async Task<ImportPromotionRunResponse> PromoteBatchAsync(HttpClient client, TestContext context, Guid batchId)
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

    private static async Task<RejectedStagingSummaryResponse> RejectStagingAsync(HttpClient client, TestContext context, Guid batchId)
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

    private static async Task<BomComparisonRunResponse> CreateBomComparisonAsync(HttpClient client, TestContext context, Guid batchId)
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

    private static async Task<GraphSnapshotContract> CaptureSnapshotAsync(HttpClient client, TestContext context, GraphSpace graphSpace)
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

    private static async Task<GraphDiffContract> CreateGraphDiffAsync(HttpClient client, TestContext context, Guid fromSnapshotId, Guid toSnapshotId)
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

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }

    private static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record TestContext(Guid TenantId, Guid UserId);

    private sealed class RecordingGraphMemoryService : IGraphMemoryService
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

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken)
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
            throw new NotSupportedException();
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
                var promoted = await CreateNodeAsync(new CreateGraphNodeRequest(tenantId, GraphSpace.Trusted, node.ObjectType, TrustState.Trusted, node.Attributes, node.SourceReference), cancellationToken);
                nodeMap[node.NodeId] = promoted.NodeId;
            }

            var promotedRelationshipIds = new List<Guid>();
            foreach (var relationship in staging.Relationships)
            {
                if (!nodeMap.TryGetValue(relationship.FromNodeId, out var fromNodeId) || !nodeMap.TryGetValue(relationship.ToNodeId, out var toNodeId))
                {
                    continue;
                }

                var promoted = await CreateRelationshipAsync(new CreateGraphRelationshipRequest(tenantId, fromNodeId, toNodeId, relationship.RelationshipType, TrustState.Trusted, relationship.Attributes, relationship.SourceReference), cancellationToken);
                promotedRelationshipIds.Add(promoted.RelationshipId);
            }

            return new GraphPromotionCopyResult(nodeMap.Values.ToList(), promotedRelationshipIds);
        }
    }
}
