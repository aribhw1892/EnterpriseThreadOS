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

public sealed class GovernedQueryTests
{
    [Fact]
    public async Task FixedQueryIntentCreatesRetrievalRunAndContextPackage()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new RecordingGraphMemoryService(context.GraphNodeId);
        var service = CreateService(dbContext, context, graph);

        var run = await service.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", "Pump context", 2),
            CancellationToken.None);

        Assert.Equal("Completed", run.Status);
        Assert.NotNull(run.ContextPackage);
        Assert.Equal(1, await dbContext.RetrievalRuns.CountAsync());
        Assert.Equal(1, await dbContext.ContextPackages.CountAsync());
        Assert.Contains(run.ContextPackage!.LlmVisibleContext, item => item.SourceKind == "Graph");
        Assert.Contains(run.ContextPackage.LlmVisibleContext, item => item.SourceKind == "Document");
    }

    [Fact]
    public async Task ContextAssemblyKeepsGraphBeforeDocumentContext()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new RecordingGraphMemoryService(context.GraphNodeId);
        var service = CreateService(dbContext, context, graph);

        var run = await service.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", null, 2),
            CancellationToken.None);

        var visible = run.ContextPackage!.LlmVisibleContext.ToList();
        Assert.True(visible.FindIndex(item => item.SourceKind == "Graph") < visible.FindIndex(item => item.SourceKind == "Document"));
    }

    [Fact]
    public async Task RestrictedAndUntrustedContextAreExcludedBeforeLlmContext()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new RecordingGraphMemoryService(context.GraphNodeId);
        var service = CreateService(dbContext, context, graph, new FilteringPolicyService());

        var run = await service.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", null, 2),
            CancellationToken.None);

        var package = run.ContextPackage!;
        Assert.DoesNotContain(package.LlmVisibleContext, item => item.ClassificationKey == "secret");
        Assert.DoesNotContain(package.LlmVisibleContext, item => item.SafeSummary.Contains("provisional", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(package.DeniedSummaries, item => item.ContextType == "part" && item.Reason == "Restricted context.");
        Assert.Contains(package.SensitiveDeniedReferences, item => item.ClassificationKey == "secret");
        Assert.Contains(package.AccessDecisions, item => item.Result == ContextDecisionResult.Denied);
    }

    [Fact]
    public async Task DocumentEvidenceIntentCanStartFromDocumentArtifact()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var service = CreateService(dbContext, context, new RecordingGraphMemoryService(context.GraphNodeId));

        var run = await service.RunAsync(
            new RunGovernedQueryRequest("document-evidence-context", null, context.DocumentArtifactId, "published-policy", null, 1),
            CancellationToken.None);

        Assert.All(run.ContextPackage!.LlmVisibleContext, item => Assert.Equal("Document", item.SourceKind));
        Assert.Equal(context.DocumentArtifactId.ToString(), run.ContextPackage.LlmVisibleContext.Single().DocumentId);
    }

    [Fact]
    public async Task CrossTenantRunAccessIsDenied()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var service = CreateService(dbContext, context, new RecordingGraphMemoryService(context.GraphNodeId));
        var run = await service.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", null, 2),
            CancellationToken.None);
        var otherContext = context with { TenantId = Guid.NewGuid() };
        var otherTenantService = CreateService(dbContext, otherContext, new RecordingGraphMemoryService(context.GraphNodeId));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => otherTenantService.GetRunAsync(run.Id, CancellationToken.None));
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

    private static GovernedQueryService CreateService(
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
            new NoOpAiTraceRecorder());
    }

    private sealed record TestContext(Guid TenantId, Guid UserId, Guid GraphNodeId, Guid ArtifactId, Guid DocumentArtifactId);

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
        public List<string> Reasons { get; } = [];

        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken)
        {
            Reasons.Add(reason);
            return Task.CompletedTask;
        }
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

    private sealed class NoOpAiTraceRecorder : IAiTraceRecorder
    {
        public Task<Guid> CreateFromRetrievalRunAsync(Guid retrievalRunId, Guid? auditRecordId, CancellationToken cancellationToken)
            => Task.FromResult(Guid.NewGuid());
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
