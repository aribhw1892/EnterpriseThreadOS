using System.Net.Http.Json;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Imports;
using ETOS.Backend.Tests.Fixtures;
using ImportFlowContext = ETOS.Backend.Tests.Fixtures.ImportFlowTestSupport.ImportFlowContext;
using RecordingGraphMemoryService = ETOS.Backend.Tests.Fixtures.ImportFlowTestSupport.RecordingGraphMemoryService;

namespace ETOS.Backend.Tests;

public sealed class ImportIdentityKeyedStagingTests
{
    [Fact]
    public async Task HasVersionRowsWithSameParentReuseSingleStagedPartNode()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = ImportFlowTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StageFlatBatchAsync(
            client,
            context,
            "documentId,fileName\n2,part2.sldprt\n",
            [
                new CreateImportColumnMappingRequest("documentId", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("fileName", "part", "partNumber", false, false)
            ],
            []);
        await StageFlatBatchAsync(
            client,
            context,
            "pdmVersionKey,documentId,lifecycleState\n2-1,2,released\n2-2,2,released\n",
            [
                new CreateImportColumnMappingRequest("pdmVersionKey", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("documentId", "partVersion", "documentId", false, false),
                new CreateImportColumnMappingRequest("lifecycleState", "partVersion", "status", false, false)
            ],
            [new CreateImportLifecycleMappingRequest("released", "released")]);

        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, "parent,child\n2,2-1\n2,2-2\n");
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "hasVersion");
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, mapping.Id, "hasVersion");

        var staging = await ImportFlowTestSupport.StageBatchAsync(client, context, batch.Id);

        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        Assert.Equal(2, staging.RelationshipCount);
        Assert.Single(graphMemory.Nodes.Where(node =>
            node.ObjectType == "part"
            && node.GraphSpace == GraphSpace.Staging
            && node.Attributes.TryGetValue("documentId", out var documentId)
            && documentId == "2"));
        Assert.Equal(2, graphMemory.Nodes.Count(node =>
            node.ObjectType == "partVersion"
            && node.GraphSpace == GraphSpace.Staging));
        Assert.Equal(2, graphMemory.Relationships.Count(relationship => relationship.RelationshipType == "HAS_VERSION"));
    }

    [Fact]
    public async Task StructuralImportWithoutEndpointsPersistsWarningAndSkipsRelationship()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = ImportFlowTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);
        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, "parent,child\n15,15-10\n");
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "hasVersion");
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, mapping.Id, "hasVersion");

        var staging = await ImportFlowTestSupport.StageBatchAsync(client, context, batch.Id);
        var batchDetail = await ImportFlowTestSupport.GetBatchDetailAsync(client, context, batch.Id);

        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        Assert.Equal(0, staging.RelationshipCount);
        Assert.Contains(batchDetail.ValidationIssues, issue => issue.IssueCode == "structural-endpoint-missing");
        Assert.Empty(graphMemory.Relationships);
    }

    [Fact]
    public async Task PromotingAllPdmBatchesProducesSingleTrustedPartForDuplicateDocumentId()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = ImportFlowTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        var partBatch = await StageFlatBatchAndReturnBatchAsync(
            client,
            context,
            "documentId,fileName\n2,part2.sldprt\n",
            [
                new CreateImportColumnMappingRequest("documentId", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("fileName", "part", "partNumber", false, false)
            ],
            []);
        var versionBatch = await StageFlatBatchAndReturnBatchAsync(
            client,
            context,
            "pdmVersionKey,documentId,lifecycleState\n2-1,2,released\n2-2,2,released\n",
            [
                new CreateImportColumnMappingRequest("pdmVersionKey", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("documentId", "partVersion", "documentId", false, false),
                new CreateImportColumnMappingRequest("lifecycleState", "partVersion", "status", false, false)
            ],
            [new CreateImportLifecycleMappingRequest("released", "released")]);
        var hasVersionBatch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, hasVersionBatch.Id, "parent,child\n2,2-1\n2,2-2\n");
        var hasVersionMapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            hasVersionBatch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "hasVersion");
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, hasVersionMapping.Id, "hasVersion");
        await ImportFlowTestSupport.StageBatchAsync(client, context, hasVersionBatch.Id);

        await ImportFlowTestSupport.PromoteBatchAsync(client, context, partBatch.Id);
        await ImportFlowTestSupport.PromoteBatchAsync(client, context, versionBatch.Id);
        await ImportFlowTestSupport.PromoteBatchAsync(client, context, hasVersionBatch.Id);

        Assert.Single(graphMemory.Nodes.Where(node =>
            node.ObjectType == "part"
            && node.GraphSpace == GraphSpace.Trusted
            && node.Attributes.TryGetValue("documentId", out var documentId)
            && documentId == "2"
            && node.Attributes.TryGetValue("partNumber", out var partNumber)
            && partNumber == "part2.sldprt"));
        Assert.Equal(2, graphMemory.Nodes.Count(node =>
            node.ObjectType == "partVersion"
            && node.GraphSpace == GraphSpace.Trusted));
        Assert.Equal(2, graphMemory.Relationships.Count(relationship =>
            relationship.RelationshipType == "HAS_VERSION"
            && graphMemory.Nodes.Any(node => node.NodeId == relationship.FromNodeId && node.GraphSpace == GraphSpace.Trusted)));
    }

    private static async Task StageFlatBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        string csv,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        IReadOnlyCollection<CreateImportLifecycleMappingRequest> lifecycleMappings)
    {
        _ = await StageFlatBatchAndReturnBatchAsync(client, context, csv, columnMappings, lifecycleMappings);
    }

    private static async Task<ImportBatchResponse> StageFlatBatchAndReturnBatchAsync(
        HttpClient client,
        ImportFlowContext context,
        string csv,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        IReadOnlyCollection<CreateImportLifecycleMappingRequest> lifecycleMappings)
    {
        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, csv);
        using var mappingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batch.Id,
                "1.0.0",
                "Flat mapping.",
                columnMappings,
                lifecycleMappings))
        };
        ImportFlowTestSupport.AddTenantHeaders(mappingRequest, context.TenantId, context.UserId);
        var mappingResponse = await client.SendAsync(mappingRequest);
        Assert.True(mappingResponse.IsSuccessStatusCode, await mappingResponse.Content.ReadAsStringAsync());
        var mapping = await mappingResponse.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.NotNull(mapping);
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, mapping.Id);
        var staging = await ImportFlowTestSupport.StageBatchAsync(client, context, batch.Id);
        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        return batch;
    }
}
