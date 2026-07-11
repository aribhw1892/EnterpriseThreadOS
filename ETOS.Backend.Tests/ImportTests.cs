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
using ETOS.Backend.Tests.Fixtures;
using ImportFlowContext = ETOS.Backend.Tests.Fixtures.ImportFlowTestSupport.ImportFlowContext;
using RecordingGraphMemoryService = ETOS.Backend.Tests.Fixtures.ImportFlowTestSupport.RecordingGraphMemoryService;
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
        await UploadCsvAsync(client, context, batch.Id, "pdmVersionKey,status,quantity\n,unknown,not-a-number\n");

        using var mappingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batch.Id,
                "1.0.0",
                "Test mapping.",
                [
                    new CreateImportColumnMappingRequest("pdmVersionKey", "partVersion", "pdmVersionKey", true, true),
                    new CreateImportColumnMappingRequest("quantity", "partVersion", "quantity", false, false)
                ],
                [new CreateImportLifecycleMappingRequest("released", "released")]))
        };
        AddTenantHeaders(mappingRequest, context.TenantId, context.UserId);
        var mappingResponse = await client.SendAsync(mappingRequest);
        Assert.True(mappingResponse.StatusCode == HttpStatusCode.OK, await mappingResponse.Content.ReadAsStringAsync());
        var mapping = await mappingResponse.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.NotNull(mapping);
        await ApproveMappingAsync(client, context, mapping.Id);

        var validation = await ValidateBatchAsync(client, context, batch.Id);

        Assert.False(validation.IsValid);
        Assert.Equal(3, validation.ErrorCount);
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "missing_required_value");
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "invalid_value_type");
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "invalid_lifecycle_value");
    }

    [Fact]
    public async Task ValidationPersistsSuspiciousNumericWarningsAndAllowsStaging()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "partNumber,lifecycle,cost\nP-100,released,-12.50\n");
        var mapping = await CreateMappingAsync(client, context, batch.Id, lifecycleValues: ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);

        var validation = await ValidateBatchAsync(client, context, batch.Id);
        var stagingRun = await StageBatchAsync(client, context, batch.Id);

        Assert.True(validation.IsValid);
        Assert.Equal(0, validation.ErrorCount);
        Assert.Equal(1, validation.WarningCount);
        Assert.Contains(validation.Issues, issue => issue.IssueCode == "suspicious_numeric_value");
        Assert.Equal(ImportStagingRunStatus.Completed, stagingRun.Status);
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
        await StageFlatBatchAsync(
            client,
            context,
            "partNumber,lifecycle,cost\nA,released,1\nB,released,1\nC,released,1\nD,released,1\n",
            [
                new CreateImportColumnMappingRequest("partNumber", "part", "partNumber", true, true),
                new CreateImportColumnMappingRequest("cost", "part", "cost", false, false)
            ],
            [new CreateImportLifecycleMappingRequest("released", "released")]);
        var batch = await CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        var csv = "bomSide,partNumber,lifecycle,cost,parent,child,quantity,unit,usage\nCAD,A,released,1,A,B,2,ea,R1\nEBOM,A,released,1,A,B,3,ea,R2\nCAD,A,released,1,A,C,1,ea,R3\nEBOM,A,released,1,A,D,1,ea,R4\n";
        await UploadCsvAsync(client, context, batch.Id, csv);
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("partNumber", "part", "partNumber", true, true),
                new CreateImportColumnMappingRequest("cost", "part", "cost", false, false),
                new CreateImportColumnMappingRequest("parent", "part", "partNumber", true, true),
                new CreateImportColumnMappingRequest("child", "part", "partNumber", true, true)
            ],
            lifecycleMappings: [new CreateImportLifecycleMappingRequest("released", "released")]);
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
    public async Task StructuralHasVersionImportLinksExistingPartAndPartVersion()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        await StageFlatBatchAsync(
            client,
            context,
            "documentId,fileName\n15,part15.sldprt\n",
            [
                new CreateImportColumnMappingRequest("documentId", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("fileName", "part", "partNumber", false, false)
            ],
            []);
        await StageFlatBatchAsync(
            client,
            context,
            "pdmVersionKey,documentId,lifecycleState\n15-10,15,released\n",
            [
                new CreateImportColumnMappingRequest("pdmVersionKey", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("documentId", "partVersion", "documentId", false, false),
                new CreateImportColumnMappingRequest("lifecycleState", "partVersion", "status", false, false)
            ],
            [new CreateImportLifecycleMappingRequest("released", "released")]);
        var batch = await CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await UploadCsvAsync(client, context, batch.Id, "parent,child\n15,15-10\n");
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "hasVersion");
        await ApproveMappingAsync(client, context, mapping.Id, "hasVersion");

        var staging = await StageBatchAsync(client, context, batch.Id);

        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        Assert.Equal(1, staging.RelationshipCount);
        Assert.Single(graphMemory.Nodes.Where(node => node.ObjectType == "part" && node.Attributes.TryGetValue("documentId", out var documentId) && documentId == "15"));
        Assert.Contains(graphMemory.CreatedRelationshipRequests, request => request.RelationshipType == "HAS_VERSION");
    }

    [Fact]
    public async Task StructuralPartVersionContainsImportStagesVersionBomRelationship()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        await StageFlatBatchAsync(
            client,
            context,
            "pdmVersionKey,documentId,lifecycleState\n15-10,15,released\n6-4,6,released\n",
            [
                new CreateImportColumnMappingRequest("pdmVersionKey", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("documentId", "partVersion", "documentId", false, false),
                new CreateImportColumnMappingRequest("lifecycleState", "partVersion", "status", false, false)
            ],
            [new CreateImportLifecycleMappingRequest("released", "released")]);
        var batch = await CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await UploadCsvAsync(client, context, batch.Id, "parent,child,quantity\n15-10,6-4,2\n");
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "contains");
        await ApproveMappingAsync(client, context, mapping.Id, "contains");

        var staging = await StageBatchAsync(client, context, batch.Id);

        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        Assert.Equal(1, staging.RelationshipCount);
        Assert.Contains(graphMemory.CreatedRelationshipRequests, request => request.RelationshipType == "BOM_CONTAINS");
        Assert.Equal("2", graphMemory.CreatedRelationshipRequests.Single().Attributes?["quantity"]);
    }

    [Fact]
    public async Task StructuralRelationshipTypeMismatchIsRejectedOnApprove()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        var batch = await CreateImportBatchAsync(client, context);
        await UploadCsvAsync(client, context, batch.Id, "parent,child\n15,15-10\n");
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "contains");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mapping.Id}/approve")
        {
            Content = JsonContent.Create(new ApproveImportMappingRequest("Approved by test.", "contains"))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        => ImportFlowTestSupport.CreateApplication(graphMemory);

    private static async Task<ImportFlowContext> CreatePublishedModelContextAsync(
        HttpClient client,
        string tenantIdentifier = "tenant-a",
        string email = "admin@example.test")
        => await ImportFlowTestSupport.CreatePublishedModelContextAsync(client, tenantIdentifier, email);

    private static Task<ImportBatchResponse> CreateImportBatchAsync(HttpClient client, ImportFlowContext context, string? sourceSystem = null)
        => ImportFlowTestSupport.CreateImportBatchAsync(client, context, sourceSystem ?? "demo-pdm");

    private static Task<UploadImportFileResponse> UploadCsvAsync(HttpClient client, ImportFlowContext context, Guid batchId, string csv)
        => ImportFlowTestSupport.UploadCsvAsync(client, context, batchId, csv);

    private static Task<ImportMappingVersionResponse> CreateMappingAsync(HttpClient client, ImportFlowContext context, Guid batchId, IReadOnlyCollection<string> lifecycleValues)
        => ImportFlowTestSupport.CreateMappingAsync(client, context, batchId, lifecycleValues);

    private static Task<ImportMappingVersionResponse> ApproveMappingAsync(HttpClient client, ImportFlowContext context, Guid mappingId, string? structuralRelationshipType = null)
        => ImportFlowTestSupport.ApproveMappingAsync(client, context, mappingId, structuralRelationshipType);

    private static Task<ImportValidationResponse> ValidateBatchAsync(HttpClient client, ImportFlowContext context, Guid batchId)
        => ImportFlowTestSupport.ValidateBatchAsync(client, context, batchId);

    private static Task<ImportStagingGraphRunResponse> StageBatchAsync(HttpClient client, ImportFlowContext context, Guid batchId)
        => ImportFlowTestSupport.StageBatchAsync(client, context, batchId);

    private static Task<ImportPromotionRunResponse> PromoteBatchAsync(HttpClient client, ImportFlowContext context, Guid batchId)
        => ImportFlowTestSupport.PromoteBatchAsync(client, context, batchId);

    private static Task<RejectedStagingSummaryResponse> RejectStagingAsync(HttpClient client, ImportFlowContext context, Guid batchId)
        => ImportFlowTestSupport.RejectStagingAsync(client, context, batchId);

    private static Task<BomComparisonRunResponse> CreateBomComparisonAsync(HttpClient client, ImportFlowContext context, Guid batchId)
        => ImportFlowTestSupport.CreateBomComparisonAsync(client, context, batchId);

    private static Task<GraphSnapshotContract> CaptureSnapshotAsync(HttpClient client, ImportFlowContext context, GraphSpace graphSpace)
        => ImportFlowTestSupport.CaptureSnapshotAsync(client, context, graphSpace);

    private static Task<GraphDiffContract> CreateGraphDiffAsync(HttpClient client, ImportFlowContext context, Guid fromSnapshotId, Guid toSnapshotId)
        => ImportFlowTestSupport.CreateGraphDiffAsync(client, context, fromSnapshotId, toSnapshotId);

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
        => ImportFlowTestSupport.AddTenantHeaders(request, tenantId, userId);

    private static async Task StageFlatBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        string csv,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        IReadOnlyCollection<CreateImportLifecycleMappingRequest> lifecycleMappings)
    {
        var batch = await CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await UploadCsvAsync(client, context, batch.Id, csv);
        using var mappingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batch.Id,
                "1.0.0",
                "Flat mapping.",
                columnMappings,
                lifecycleMappings))
        };
        AddTenantHeaders(mappingRequest, context.TenantId, context.UserId);
        var mappingResponse = await client.SendAsync(mappingRequest);
        Assert.True(mappingResponse.IsSuccessStatusCode, await mappingResponse.Content.ReadAsStringAsync());
        var mapping = await mappingResponse.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.NotNull(mapping);
        await ApproveMappingAsync(client, context, mapping.Id);
        var staging = await StageBatchAsync(client, context, batch.Id);
        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
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

    private static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
