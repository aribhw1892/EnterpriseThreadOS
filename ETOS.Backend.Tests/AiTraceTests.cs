using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Documents;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class AiTraceTests
{
    [Fact]
    public async Task GovernedQueryRunCreatesLinkedAiTraceRecord()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new RecordingGraphMemoryService(context.GraphNodeId);
        var governedQueryService = CreateGovernedQueryService(dbContext, context, graph);
        var aiTraceService = CreateAiTraceService(dbContext, context, new AllowAllPermissionService());

        var run = await governedQueryService.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", "Pump context", 2),
            CancellationToken.None);

        var trace = await aiTraceService.GetTraceByRetrievalRunAsync(run.Id, CancellationToken.None);
        Assert.NotNull(trace);
        Assert.Equal(run.Id, trace!.RetrievalRunId);
        Assert.Equal("object-360-context", trace.IntentKey);
        Assert.NotEmpty(trace.ArtifactLinks);
        Assert.Equal(1, await dbContext.AiTraceRecords.CountAsync());
    }

    [Fact]
    public async Task ReadPermissionAllowsListAndGetButExportRequiresExportPermission()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var traceId = await SeedTraceAsync(dbContext, context);
        var readOnlyPermissions = new FixedPermissionService([AiTracePermissions.Read]);
        var aiTraceService = CreateAiTraceService(dbContext, context, readOnlyPermissions);

        var traces = await aiTraceService.ListTracesAsync(CancellationToken.None);
        var detail = await aiTraceService.GetTraceAsync(traceId, CancellationToken.None);

        Assert.Single(traces);
        Assert.Equal(traceId, detail.Id);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => aiTraceService.ExportTraceAsync(traceId, CancellationToken.None));
    }

    [Fact]
    public async Task ExportSuccessWritesExportRecordAndAuditMetadata()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var traceId = await SeedTraceAsync(dbContext, context);
        var auditRecorder = new CapturingAuditRecorder();
        var aiTraceService = CreateAiTraceService(dbContext, context, new AllowAllPermissionService(), auditRecorder);

        var export = await aiTraceService.ExportTraceAsync(traceId, CancellationToken.None);

        Assert.NotEmpty(export.Content);
        Assert.Equal("application/json", export.ContentType);
        Assert.NotEmpty(export.Metadata.ExportHash);
        Assert.Equal("fullPermissionSafe", export.Metadata.EvidenceLevel);
        Assert.Equal(1, await dbContext.AiTraceExportRecords.CountAsync());
        Assert.Contains(auditRecorder.AuditRecords, record => record.Action == "ai_trace.export" && record.Result == AuditResult.Export);
    }

    [Fact]
    public async Task ExportDeniedCreatesExportDeniedSecurityEvent()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var traceId = await SeedTraceAsync(dbContext, context);
        var auditRecorder = new CapturingAuditRecorder();
        var aiTraceService = CreateAiTraceService(
            dbContext,
            context,
            new FixedPermissionService([AiTracePermissions.Read]),
            auditRecorder);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => aiTraceService.ExportTraceAsync(traceId, CancellationToken.None));

        Assert.Contains(auditRecorder.SecurityEvents, item => item.EventType == SecurityEventType.ExportDenied);
    }

    [Fact]
    public async Task CrossTenantTraceAccessIsDenied()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var traceId = await SeedTraceAsync(dbContext, context);
        var otherContext = context with { TenantId = Guid.NewGuid() };
        var aiTraceService = CreateAiTraceService(dbContext, otherContext, new AllowAllPermissionService());

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => aiTraceService.GetTraceAsync(traceId, CancellationToken.None));
    }

    [Fact]
    public async Task SensitiveDeniedReferencesHiddenFromReadOnlyUser()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new RecordingGraphMemoryService(context.GraphNodeId);
        var governedQueryService = CreateGovernedQueryService(dbContext, context, graph, new FilteringPolicyService());
        var aiTraceService = CreateAiTraceService(dbContext, context, new FixedPermissionService([AiTracePermissions.Read]));

        var run = await governedQueryService.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", null, 2),
            CancellationToken.None);
        var trace = await aiTraceService.GetTraceByRetrievalRunAsync(run.Id, CancellationToken.None);

        Assert.NotNull(trace);
        Assert.NotEmpty(trace!.DeniedSafeSummaries);
        Assert.Null(trace.SensitiveDeniedReferences);
    }

    private static async Task<Guid> SeedTraceAsync(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        var graph = new RecordingGraphMemoryService(context.GraphNodeId);
        var governedQueryService = CreateGovernedQueryService(dbContext, context, graph);
        var run = await governedQueryService.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", null, 2),
            CancellationToken.None);
        var trace = await dbContext.AiTraceRecords.SingleAsync(item => item.RetrievalRunId == run.Id);
        return trace.Id;
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantUserAndDocument(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
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
        dbContext.Artifacts.Add(new Artifact
        {
            Id = context.ArtifactId,
            TenantId = context.TenantId,
            ArtifactType = "document",
            NormalizedArtifactType = "DOCUMENT",
            Name = "Pump spec",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.DocumentArtifacts.Add(new DocumentArtifact
        {
            Id = context.DocumentArtifactId,
            TenantId = context.TenantId,
            ArtifactId = context.ArtifactId,
            DocumentType = "spec",
            NormalizedDocumentType = "SPEC",
            ClassificationKey = "internal",
            NormalizedClassificationKey = "INTERNAL",
            Title = "Pump spec",
            OwnerUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var versionId = Guid.NewGuid();
        dbContext.DocumentVersions.Add(new DocumentVersion
        {
            Id = versionId,
            TenantId = context.TenantId,
            DocumentArtifactId = context.DocumentArtifactId,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            StorageKey = "tenant/document/file.txt",
            Sha256Checksum = new string('a', 64),
            OriginalFileName = "file.txt",
            ContentType = "text/plain",
            SizeBytes = 12,
            ExtractionStatus = DocumentExtractionStatus.MetadataImported,
            UploadedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.DocumentObjectLinks.Add(new DocumentObjectLink
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DocumentArtifactId = context.DocumentArtifactId,
            DocumentVersionId = versionId,
            GraphNodeId = context.GraphNodeId,
            ConfidenceScore = 0.98m,
            EvidenceSummary = "Document links to pump graph node.",
            ExtractionStatus = DocumentExtractionStatus.Completed,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private static GovernedQueryService CreateGovernedQueryService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGraphMemoryService graphMemoryService,
        IClassificationPolicyService? policyService = null)
    {
        return new GovernedQueryService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            graphMemoryService,
            policyService ?? new AllowAllPolicyService(),
            new AiTraceRecorder(dbContext));
    }

    private static AiTraceService CreateAiTraceService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IAccessPermissionService permissionService,
        IAuditRecorder? auditRecorder = null)
    {
        return new AiTraceService(
            dbContext,
            new StaticTenantContextResolver(context),
            permissionService,
            new RecordingDenialRecorder(),
            auditRecorder ?? new RecordingAuditRecorder());
    }

    private sealed record TestContext(Guid TenantId, Guid UserId, Guid GraphNodeId, Guid ArtifactId, Guid DocumentArtifactId);

    private sealed class StaticTenantContextResolver(TestContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
            => Task.FromResult(new ActiveTenantContext(context.TenantId, "demo", "Demo", context.UserId));
    }

    private sealed class AllowAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FixedPermissionService(IReadOnlyCollection<string> granted) : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
            => Task.FromResult(granted.Contains(permissionKey, StringComparer.OrdinalIgnoreCase));
    }

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken)
            => Task.CompletedTask;
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

    private sealed class CapturingAuditRecorder : IAuditRecorder
    {
        public List<(string Action, AuditResult Result)> AuditRecords { get; } = [];
        public List<SecurityEventWriteRequest> SecurityEvents { get; } = [];

        public Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
        {
            AuditRecords.Add((request.Action, request.Result));
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
            SecurityEvents.Add(request);
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

    private sealed class RecordingGraphMemoryService(Guid startNodeId) : IGraphMemoryService
    {
        private readonly Guid secretNodeId = Guid.NewGuid();
        private readonly Guid provisionalNodeId = Guid.NewGuid();

        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var start = new BaseNode(
                startNodeId,
                request.TenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "Trusted pump part." },
                null,
                now,
                now);
            var secret = new BaseNode(
                secretNodeId,
                request.TenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["classificationKey"] = "secret", ["safeSummary"] = "Secret cost rollup." },
                null,
                now,
                now);
            var provisional = new BaseNode(
                provisionalNodeId,
                request.TenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Provisional,
                new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "provisional supplier match." },
                null,
                now,
                now);
            var relationship = new BaseRelationship(
                Guid.NewGuid(),
                request.TenantId,
                startNodeId,
                secretNodeId,
                "BOM_CHILD",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "Trusted BOM edge." },
                null,
                now,
                now);

            return Task.FromResult(new GraphTraversalResult(start, [start, secret, provisional], [relationship]));
        }

        public Task<GraphReadModel> ListGraphAsync(Guid tenantId, GraphSpace? graphSpace, string? sourceBatchId, IReadOnlyCollection<Guid>? nodeIds, IReadOnlyCollection<Guid>? relationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphReadModel([], []));

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(Guid tenantId, IReadOnlyCollection<Guid> stagingNodeIds, IReadOnlyCollection<Guid> stagingRelationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphPromotionCopyResult([], []));
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
                .Select(item => new PolicyDeniedSummaryResponse(item.ContextId, item.ContextType, item.SafeSummary, "Restricted context.", PolicyRuleEffect.Deny))
                .ToList();
            var sensitive = request.Items
                .Where(item => string.Equals(item.ClassificationKey, "secret", StringComparison.OrdinalIgnoreCase))
                .Select(item => new PolicySensitiveDeniedReferenceResponse(item.ContextId, item.ContextType, item.DocumentId, item.ClassificationKey, item.AttributeKey, "Restricted context."))
                .ToList();

            return Task.FromResult(new PolicyEvaluationResponse(Guid.NewGuid(), Guid.NewGuid(), request.PolicyKey, "v1", allowed, denied, sensitive));
        }
    }
}
