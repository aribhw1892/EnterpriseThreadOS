using System.Net.Http.Json;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Imports;
using ETOS.Backend.Tests.Fixtures;

namespace ETOS.Backend.Tests;

public sealed class PdmImportFlowTests
{
    private static readonly CreateImportLifecycleMappingRequest[] PdmPartVersionLifecycleMappings =
    [
        new("MFG", "released"),
        new("Concept Approved", "released"),
        new("Under Detailing", "in-review"),
        new("ECN waiting for Approval", "in-review"),
        new("Waiting for Detailing Approval", "in-review"),
        new("Waiting for Approval", "in-review"),
        new("Project Under Design", "draft"),
        new("Changes in 3D Model", "draft")
    ];

    [Fact]
    public async Task DemoPdmFixtures_ImportAllFourBatchesIntoStagingGraph()
    {
        var graphMemory = new ImportFlowTestSupport.RecordingGraphMemoryService();
        await using var application = ImportFlowTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await ImportFlatBatchAsync(
            client,
            context,
            "parts.csv",
            [
                new CreateImportColumnMappingRequest("documentId", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("fileName", "part", "partNumber", false, false)
            ],
            []);

        await ImportFlatBatchAsync(
            client,
            context,
            "part-versions.csv",
            [
                new CreateImportColumnMappingRequest("pdmVersionKey", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("documentId", "partVersion", "documentId", false, false),
                new CreateImportColumnMappingRequest("revision", "partVersion", "revision", false, false),
                new CreateImportColumnMappingRequest("fileName", "partVersion", "fileName", false, false),
                new CreateImportColumnMappingRequest("lifecycleState", "partVersion", "status", false, false),
                new CreateImportColumnMappingRequest("workflow", "partVersion", "workflow", false, false),
                new CreateImportColumnMappingRequest("isLatest", "partVersion", "isLatest", false, false),
                new CreateImportColumnMappingRequest("projectPath", "partVersion", "projectPath", false, false)
            ],
            PdmPartVersionLifecycleMappings);

        await ImportStructuralBatchAsync(
            client,
            context,
            graphMemory,
            "has-version.csv",
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "hasVersion",
            "HAS_VERSION");

        await ImportStructuralBatchAsync(
            client,
            context,
            graphMemory,
            "version-bom.csv",
            [
                new CreateImportColumnMappingRequest("parent", "partVersion", "pdmVersionKey", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            "contains",
            "BOM_CONTAINS");

        Assert.Contains(graphMemory.CreatedRelationshipRequests, request => request.RelationshipType == "HAS_VERSION");
        Assert.Contains(graphMemory.CreatedRelationshipRequests, request => request.RelationshipType == "BOM_CONTAINS");
        Assert.True(graphMemory.CreatedNodeRequests.Count(request => request.ObjectType == "part") >= 2);
        Assert.True(graphMemory.CreatedNodeRequests.Count(request => request.ObjectType == "partVersion") >= 2);
    }

    private static async Task ImportFlatBatchAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        string relativePath,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        IReadOnlyCollection<CreateImportLifecycleMappingRequest> lifecycleMappings)
    {
        var csv = await ImportFlowTestSupport.ReadDemoCsvAsync(Path.Combine("pdm", relativePath));
        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM", $"PDM {relativePath}");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, csv, relativePath);

        using var mappingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batch.Id,
                relativePath,
                $"PDM flat mapping for {relativePath}.",
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
        Assert.True(staging.NodeCount > 0);
    }

    private static async Task ImportStructuralBatchAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        ImportFlowTestSupport.RecordingGraphMemoryService graphMemory,
        string relativePath,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        string structuralRelationshipType,
        string expectedRelationshipType)
    {
        var csv = await ImportFlowTestSupport.ReadDemoCsvAsync(Path.Combine("pdm", relativePath));
        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM", $"PDM {relativePath}");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, csv, relativePath);
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            columnMappings,
            structuralRelationshipType,
            versionLabel: relativePath);
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, mapping.Id, structuralRelationshipType);
        var staging = await ImportFlowTestSupport.StageBatchAsync(client, context, batch.Id);
        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        Assert.Contains(graphMemory.CreatedRelationshipRequests, request => request.RelationshipType == expectedRelationshipType);
    }
}
