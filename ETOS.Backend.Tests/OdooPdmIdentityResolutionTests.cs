using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class OdooPdmIdentityResolutionTests
{
    [Fact]
    public async Task CrossAttributeVersionMatching_GeneratesCandidates()
    {
        await using var application = ImportFlowTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StagePdmPartVersionsAsync(client, context);
        var odooBatch = await StageOdooPartVersionsAsync(client, context);

        var generated = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);

        Assert.True(generated.CreatedCount > 0);
        var candidate = generated.Candidates.FirstOrDefault(item =>
            string.Equals(item.IdentityKey, "2-1", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SourceSystem, "ODOO-ERP", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.TargetSystem, "SOLIDWORKS-PDM", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(candidate);
        Assert.Contains("sourcePdmVersionKey", candidate.EvidenceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pdmVersionKey", candidate.EvidenceSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossAttributePartMatching_GeneratesCandidates()
    {
        await using var application = ImportFlowTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StagePdmPartsAsync(client, context);
        var odooBatch = await StageOdooPartsAsync(client, context);

        var generated = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);

        Assert.True(generated.CreatedCount > 0);
        var candidate = generated.Candidates.FirstOrDefault(item =>
            string.Equals(item.IdentityKey, "2", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SourceSystem, "ODOO-ERP", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.TargetSystem, "SOLIDWORKS-PDM", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(candidate);
        Assert.Contains("sourceDocumentId", candidate.EvidenceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("documentId", candidate.EvidenceSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossAttributePartMatching_IgnoresStructuralRelationshipBatches()
    {
        await using var application = ImportFlowTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StagePdmPartsAsync(client, context);
        await StagePdmHasVersionAsync(client, context);
        var odooBatch = await StageOdooPartsAsync(client, context);

        var generated = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);

        var documentTwoCandidates = generated.Candidates
            .Where(item =>
                string.Equals(item.SourceRecordId, "ODOO-PROD-000002", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SourceSystem, "ODOO-ERP", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.TargetSystem, "SOLIDWORKS-PDM", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var candidate = Assert.Single(documentTwoCandidates);
        Assert.Equal("2", candidate.TargetRecordId);
        Assert.NotEqual(IdentityCandidateState.Conflicted, candidate.State);
        Assert.DoesNotContain(documentTwoCandidates, item => item.TargetRecordId.Contains('|', StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrossAttributeMatching_IsIdempotent()
    {
        await using var application = ImportFlowTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StagePdmPartVersionsAsync(client, context);
        var odooBatch = await StageOdooPartVersionsAsync(client, context);

        var firstRun = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);
        var secondRun = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);

        Assert.True(firstRun.CreatedCount > 0);
        Assert.Equal(0, secondRun.CreatedCount);
        Assert.Equal(firstRun.Candidates.Count, secondRun.Candidates.Count);
    }

    [Fact]
    public async Task CrossAttributeApproval_CreatesIdentityLink()
    {
        var graphMemory = new ImportFlowTestSupport.RecordingGraphMemoryService();
        await using var application = ImportFlowTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StagePdmPartVersionsAsync(client, context);
        var odooBatch = await StageOdooPartVersionsAsync(client, context);
        var generated = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);
        var candidate = generated.Candidates.First(item => string.Equals(item.IdentityKey, "2-1", StringComparison.OrdinalIgnoreCase));

        var approved = await ImportFlowTestSupport.ApproveIdentityCandidateAsync(client, context, candidate.Id);

        Assert.Equal(IdentityCandidateState.Approved, approved.State);
        Assert.NotNull(approved.GraphRelationshipId);
        var relationship = Assert.Single(graphMemory.CreatedRelationshipRequests);
        Assert.Equal("IDENTITY_LINK", relationship.RelationshipType);
    }

    [Fact]
    public async Task ApproveAllCandidates_ApprovesReviewableAndSkipsConflicted()
    {
        var graphMemory = new ImportFlowTestSupport.RecordingGraphMemoryService();
        await using var application = ImportFlowTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client);

        await StagePdmPartVersionsAsync(client, context);
        var odooBatch = await StageOdooPartVersionsAsync(client, context);
        var generated = await ImportFlowTestSupport.GenerateIdentityCandidatesAsync(client, context, odooBatch.Id);

        Assert.True(generated.CreatedCount > 0);

        var result = await ImportFlowTestSupport.ApproveAllIdentityCandidatesAsync(client, context, odooBatch.Id);

        Assert.Equal(generated.CreatedCount, result.ApprovedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.All(result.Candidates, item => Assert.Equal(IdentityCandidateState.Approved, item.State));
        Assert.Equal(generated.CreatedCount, graphMemory.CreatedRelationshipRequests.Count);
    }

    [Fact]
    public async Task Installer_SeedsCrossAttributeRules()
    {
        await using var application = ImportFlowTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var context = await ImportFlowTestSupport.CreatePublishedModelContextAsync(client, "tenant-odoo-pdm-id", "id-bridge@example.test");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var rules = await dbContext.IdentityResolutionRules
            .Where(rule => rule.TenantId == context.TenantId && rule.CrossAttributePairsJson != null)
            .ToListAsync();

        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, rule => rule.NormalizedObjectType == "PART" && rule.CrossAttributePairsJson!.Contains("sourceDocumentId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(rules, rule => rule.NormalizedObjectType == "PARTVERSION" && rule.CrossAttributePairsJson!.Contains("sourcePdmVersionKey", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task StagePdmPartVersionsAsync(HttpClient client, ImportFlowTestSupport.ImportFlowContext context)
    {
        await StageFlatBatchAsync(
            client,
            context,
            "SOLIDWORKS-PDM",
            Path.Combine("pdm", "part-versions.csv"),
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
    }

    private static async Task<ImportBatchResponse> StageOdooPartVersionsAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context)
    {
        return await StageFlatBatchAsync(
            client,
            context,
            "ODOO-ERP",
            Path.Combine("odoo", "odoo-part-versions.csv"),
            [
                new CreateImportColumnMappingRequest("odooVersionKey", "partVersion", "odooVersionKey", true, true),
                new CreateImportColumnMappingRequest("sourcePdmVersionKey", "partVersion", "sourcePdmVersionKey", false, false),
                new CreateImportColumnMappingRequest("odooProductId", "partVersion", "odooProductId", false, false),
                new CreateImportColumnMappingRequest("sourceDocumentId", "partVersion", "sourceDocumentId", false, false),
                new CreateImportColumnMappingRequest("revision", "partVersion", "revision", false, false),
                new CreateImportColumnMappingRequest("lifecycleState", "partVersion", "status", false, false),
                new CreateImportColumnMappingRequest("isCurrent", "partVersion", "isLatest", false, false),
                new CreateImportColumnMappingRequest("effectiveDate", "partVersion", "effectiveDate", false, false),
                new CreateImportColumnMappingRequest("standardPrice", "partVersion", "standardPrice", false, false)
            ],
            OdooPartVersionLifecycleMappings);
    }

    private static async Task StagePdmPartsAsync(HttpClient client, ImportFlowTestSupport.ImportFlowContext context)
    {
        await StageFlatBatchAsync(
            client,
            context,
            "SOLIDWORKS-PDM",
            Path.Combine("pdm", "parts.csv"),
            [
                new CreateImportColumnMappingRequest("documentId", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("fileName", "part", "partNumber", false, false)
            ],
            []);
    }

    private static async Task StagePdmHasVersionAsync(HttpClient client, ImportFlowTestSupport.ImportFlowContext context)
    {
        var csv = await ImportFlowTestSupport.ReadDemoCsvAsync(Path.Combine("pdm", "has-version.csv"));
        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, "SOLIDWORKS-PDM", "has-version.csv");
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, csv, "has-version.csv");
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            [
                new CreateImportColumnMappingRequest("parent", "part", "documentId", true, true),
                new CreateImportColumnMappingRequest("child", "partVersion", "pdmVersionKey", true, true)
            ],
            structuralRelationshipType: "hasVersion",
            versionLabel: "has-version.csv");
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, mapping.Id, "hasVersion");
        await ImportFlowTestSupport.ValidateBatchAsync(client, context, batch.Id);
        var staging = await ImportFlowTestSupport.StageBatchAsync(client, context, batch.Id);
        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
    }

    private static async Task<ImportBatchResponse> StageOdooPartsAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context)
    {
        return await StageFlatBatchAsync(
            client,
            context,
            "ODOO-ERP",
            Path.Combine("odoo", "odoo-parts.csv"),
            [
                new CreateImportColumnMappingRequest("odooProductId", "part", "odooProductId", true, true),
                new CreateImportColumnMappingRequest("sourceDocumentId", "part", "sourceDocumentId", false, false),
                new CreateImportColumnMappingRequest("defaultCode", "part", "partNumber", false, false),
                new CreateImportColumnMappingRequest("name", "part", "name", false, false),
                new CreateImportColumnMappingRequest("productType", "part", "productType", false, false),
                new CreateImportColumnMappingRequest("uom", "part", "uom", false, false),
                new CreateImportColumnMappingRequest("productCategory", "part", "category", false, false),
                new CreateImportColumnMappingRequest("route", "part", "route", false, false),
                new CreateImportColumnMappingRequest("active", "part", "active", false, false),
                new CreateImportColumnMappingRequest("companyCode", "part", "companyCode", false, false)
            ],
            []);
    }

    private static async Task<ImportBatchResponse> StageFlatBatchAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        string sourceSystem,
        string relativePath,
        IReadOnlyCollection<CreateImportColumnMappingRequest> columnMappings,
        IReadOnlyCollection<CreateImportLifecycleMappingRequest> lifecycleMappings)
    {
        var csv = await ImportFlowTestSupport.ReadDemoCsvAsync(relativePath);
        var batch = await ImportFlowTestSupport.CreateImportBatchAsync(client, context, sourceSystem, relativePath);
        await ImportFlowTestSupport.UploadCsvAsync(client, context, batch.Id, csv, Path.GetFileName(relativePath));
        var mapping = await ImportFlowTestSupport.CreateStructuralMappingAsync(
            client,
            context,
            batch.Id,
            columnMappings,
            structuralRelationshipType: null,
            versionLabel: Path.GetFileName(relativePath),
            lifecycleMappings: lifecycleMappings);
        await ImportFlowTestSupport.ApproveMappingAsync(client, context, mapping.Id);
        await ImportFlowTestSupport.ValidateBatchAsync(client, context, batch.Id);
        var staging = await ImportFlowTestSupport.StageBatchAsync(client, context, batch.Id);
        Assert.Equal(ImportStagingRunStatus.Completed, staging.Status);
        return batch;
    }

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

    private static readonly CreateImportLifecycleMappingRequest[] OdooPartVersionLifecycleMappings =
    [
        new("released", "released"),
        new("in_review", "in-review"),
        new("draft", "draft")
    ];
}
