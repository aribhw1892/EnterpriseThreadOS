using System.Security.Cryptography;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Documents;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class DocumentMemoryTests
{
    [Fact]
    public async Task DocumentCreationAndVersionUploadCreateArtifactAndMetadata()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndImport(dbContext);
        var service = CreateService(dbContext, context);

        var document = await service.CreateDocumentAsync(
            new CreateDocumentArtifactRequest("engineering-spec", "internal", "Pump spec", "Assembly spec.", null),
            CancellationToken.None);
        var version = await service.AddVersionAsync(
            document.Id,
            CreateTextFile("pump-spec.txt", "pump assembly text"),
            new CreateDocumentVersionRequest("v1", """{"summary":"Pump metadata"}""", DocumentExtractionStatus.MetadataImported, null),
            CancellationToken.None);

        Assert.Equal("document", (await dbContext.Artifacts.SingleAsync()).ArtifactType);
        Assert.Equal(document.Id, version.DocumentArtifactId);
        Assert.Equal(Sha256("pump assembly text"), version.Sha256Checksum);
        Assert.DoesNotContain("pump assembly text", version.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentExtractionStatus.MetadataImported, version.ExtractionStatus);
    }

    [Fact]
    public async Task UncertainDocumentLinksCreateReviewableDataQualityIssues()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndImport(dbContext);
        var service = CreateService(dbContext, context);
        var document = await CreateDocumentWithVersionAsync(service);
        var batch = await dbContext.ImportBatches.SingleAsync();

        var link = await service.CreateLinkAsync(
            document.Id,
            new CreateDocumentObjectLinkRequest(
                document.Versions.Single().Id,
                null,
                batch.Id,
                0.60m,
                "Uncertain document/import evidence link.",
                DocumentExtractionStatus.Uncertain,
                "demo-pdm",
                batch.Id.ToString()),
            CancellationToken.None);

        var issue = await dbContext.DataQualityIssues
            .Include(item => item.SourceLinks)
            .SingleAsync();
        Assert.Equal(link.Id.ToString(), issue.SourceLinks.Single(source => source.SourceType == DataQualitySourceLinkType.DocumentObjectLink).SourceId);
        Assert.Equal(DataQualityIssueOrigin.DocumentExtraction, issue.Origin);
        Assert.Equal(DataQualityAffectedEntityType.DocumentObjectLink, issue.AffectedEntityType);
        Assert.True(issue.ReviewTaskReady);
    }

    [Fact]
    public async Task ExtractionFailuresCreateDocumentVersionIssues()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndImport(dbContext);
        var service = CreateService(dbContext, context);
        var document = await service.CreateDocumentAsync(
            new CreateDocumentArtifactRequest("drawing", "internal", "Failed drawing", null, null),
            CancellationToken.None);

        await service.AddVersionAsync(
            document.Id,
            CreateTextFile("drawing.txt", "bad extraction"),
            new CreateDocumentVersionRequest("v1", null, DocumentExtractionStatus.Failed, "OCR failed."),
            CancellationToken.None);

        var issue = await dbContext.DataQualityIssues
            .Include(item => item.SourceLinks)
            .SingleAsync();
        Assert.Equal(DataQualityAffectedEntityType.DocumentVersion, issue.AffectedEntityType);
        Assert.Contains(issue.SourceLinks, link => link.SourceType == DataQualitySourceLinkType.DocumentVersion);
        Assert.True(issue.ExcludedFromTrustedRecommendations);
    }

    [Fact]
    public async Task VectorIndexRequestsRecordDisabledProviderWithTenantAndPolicyMetadata()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndImport(dbContext);
        var service = CreateService(dbContext, context);
        var document = await CreateDocumentWithVersionAsync(service);
        var version = document.Versions.Single();

        var record = await service.RequestVectorIndexAsync(
            document.Id,
            version.Id,
            new CreateDocumentVectorIndexRequest(null, "Safe vector summary."),
            CancellationToken.None);

        Assert.Equal(DocumentVectorIndexStatus.DisabledPlaceholder, record.Status);
        Assert.Equal(context.TenantId.ToString(), record.TenantFilter);
        Assert.Contains("classification=internal", record.PolicyFilterSummary);
        Assert.Equal(1, await dbContext.DocumentVectorIndexRecords.CountAsync());
    }

    [Fact]
    public async Task PolicyFilteringExcludesRestrictedDocuments()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndImport(dbContext);
        var service = CreateService(dbContext, context, policyService: new FilteringPolicyService());
        await service.CreateDocumentAsync(
            new CreateDocumentArtifactRequest("spec", "internal", "Allowed spec", null, null),
            CancellationToken.None);
        await service.CreateDocumentAsync(
            new CreateDocumentArtifactRequest("spec", "secret", "Restricted spec", null, null),
            CancellationToken.None);

        var documents = await service.ListDocumentsAsync("published-policy", CancellationToken.None);

        Assert.Single(documents);
        Assert.Equal("Allowed spec", documents.Single().Title);
    }

    [Fact]
    public async Task CadParsingPlaceholderStaysDisabled()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndImport(dbContext);
        var service = CreateService(dbContext, context);

        var status = await service.GetCadParsingStatusAsync(CancellationToken.None);

        Assert.False(status.IsEnabled);
        Assert.Contains("deferred", status.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<DocumentArtifactDetailResponse> CreateDocumentWithVersionAsync(IDocumentService service)
    {
        var document = await service.CreateDocumentAsync(
            new CreateDocumentArtifactRequest("engineering-spec", "internal", "Pump spec", "Assembly spec.", null),
            CancellationToken.None);
        await service.AddVersionAsync(
            document.Id,
            CreateTextFile("pump-spec.txt", "pump assembly text"),
            new CreateDocumentVersionRequest("v1", """{"summary":"Pump metadata"}""", DocumentExtractionStatus.MetadataImported, null),
            CancellationToken.None);

        return await service.GetDocumentAsync(document.Id, null, CancellationToken.None);
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantUserAndImport(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid());
        dbContext.Users.Add(new EtosUser
        {
            Id = context.UserId,
            UserName = "admin@example.test",
            NormalizedUserName = "ADMIN@EXAMPLE.TEST",
            Email = "admin@example.test",
            NormalizedEmail = "ADMIN@EXAMPLE.TEST",
            DisplayName = "Admin User",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ImportBatches.Add(new ImportBatch
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceSystem = "demo-pdm",
            NormalizedSourceSystem = "DEMO-PDM",
            Description = "Demo import.",
            Status = ImportBatchStatus.Promoted,
            ActiveModelPackageVersionId = Guid.NewGuid(),
            ActiveModelPackageKey = "canonical-manufacturing-package",
            ActiveModelPackageVersionLabel = "v1",
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private static DocumentService CreateService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IClassificationPolicyService? policyService = null)
    {
        return new DocumentService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new RecordingDocumentFileStorage(),
            new DisabledDocumentVectorIndexingService(),
            new DisabledCadParsingPlaceholder(),
            new RecordingGraphMemoryService(),
            policyService ?? new AllowAllPolicyService());
    }

    private static IFormFile CreateTextFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed record TestContext(Guid TenantId, Guid UserId);

    private sealed class StaticTenantContextResolver(TestContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ActiveTenantContext(context.TenantId, "demo", "Demo", context.UserId));
        }
    }

    private sealed class AllowAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingAuditRecorder : IAuditRecorder
    {
        public Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuditRecordResponse(
                Guid.NewGuid(),
                request.TenantId,
                request.UserId,
                request.Action,
                request.Result,
                request.Reason,
                request.SourceObjectType,
                request.SourceObjectId,
                request.PolicyName,
                request.PolicyVersion,
                null,
                request.SafeSummary,
                request.RetentionCategory,
                request.RetainUntil,
                request.IsArchiveEligible,
                null,
                DateTimeOffset.UtcNow));
        }

        public Task<SecurityEventResponse> RecordSecurityEventAsync(SecurityEventWriteRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SecurityEventResponse(
                Guid.NewGuid(),
                request.TenantId,
                request.UserId,
                request.EventType,
                request.Severity,
                request.SourceAction,
                request.Reason,
                request.SafeSummary,
                request.RelatedAuditRecordId,
                request.ReviewTaskReady,
                request.ReviewTaskHint,
                null,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class RecordingDocumentFileStorage : IDocumentFileStorage
    {
        public async Task<StoredDocumentFile> StoreAsync(Guid tenantId, Guid documentId, string originalFileName, Stream content, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            var checksum = Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
            return new StoredDocumentFile($"{tenantId:N}/{documentId:N}/{Path.GetFileName(originalFileName)}", checksum, memory.Length);
        }
    }

    private sealed class RecordingGraphMemoryService : IGraphMemoryService
    {
        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken)
        {
            return Task.FromResult<BaseNode?>(new BaseNode(nodeId, tenantId, GraphSpace.Trusted, "part", TrustState.Trusted, new Dictionary<string, string?>(), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GraphReadModel> ListGraphAsync(
            Guid tenantId,
            GraphSpace? graphSpace,
            string? sourceBatchId,
            IReadOnlyCollection<Guid>? nodeIds,
            IReadOnlyCollection<Guid>? relationshipIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new GraphReadModel([], []));
        }

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(Guid tenantId, IReadOnlyCollection<Guid> stagingNodeIds, IReadOnlyCollection<Guid> stagingRelationshipIds, CancellationToken cancellationToken)
        {
            return Task.FromResult(new GraphPromotionCopyResult([], []));
        }
    }

    private class AllowAllPolicyService : IClassificationPolicyService
    {
        public virtual Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PolicyEvaluationResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request.PolicyKey,
                "v1",
                request.Items.Select(item => new PolicyAllowedContextResponse(item.ContextId, item.ContextType, item.SafeSummary)).ToList(),
                [],
                []));
        }

        public Task<IReadOnlyCollection<ClassificationSchemeResponse>> ListSchemesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClassificationSchemeResponse> CreateSchemeAsync(CreateClassificationSchemeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ClassificationSchemeVersionResponse>> ListSchemeVersionsAsync(Guid schemeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClassificationSchemeVersionResponse> CreateSchemeVersionAsync(Guid schemeId, CreateClassificationSchemeVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClassificationSchemeVersionResponse> PublishSchemeVersionAsync(Guid schemeId, Guid versionId, PublishClassificationSchemeVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PolicyVersionResponse>> ListPolicyVersionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PolicyVersionResponse> CreatePolicyVersionAsync(CreatePolicyVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<RestrictedContextRuleResponse>> ListRestrictedRulesAsync(Guid? policyVersionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RestrictedContextRuleResponse> AddRestrictedRuleAsync(Guid policyVersionId, CreateRestrictedContextRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PolicyVersionResponse> PublishPolicyVersionAsync(Guid policyVersionId, PublishPolicyVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PolicyImpactResponse> GetPolicyImpactAsync(Guid policyVersionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ArtifactPolicyRiskStatus> EvaluateArtifactPolicyRiskAsync(Guid tenantId, Guid artifactVersionId, CancellationToken cancellationToken) => Task.FromResult(ArtifactPolicyRiskStatus.Acceptable);
    }

    private sealed class FilteringPolicyService : AllowAllPolicyService
    {
        public override Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken)
        {
            var allowed = request.Items
                .Where(item => !string.Equals(item.ClassificationKey, "secret", StringComparison.OrdinalIgnoreCase))
                .Select(item => new PolicyAllowedContextResponse(item.ContextId, item.ContextType, item.SafeSummary))
                .ToList();
            var denied = request.Items
                .Where(item => string.Equals(item.ClassificationKey, "secret", StringComparison.OrdinalIgnoreCase))
                .Select(item => new PolicyDeniedSummaryResponse(item.ContextId, item.ContextType, item.SafeSummary, "Restricted document.", PolicyRuleEffect.Deny))
                .ToList();

            return Task.FromResult(new PolicyEvaluationResponse(Guid.NewGuid(), Guid.NewGuid(), request.PolicyKey, "v1", allowed, denied, []));
        }
    }
}
