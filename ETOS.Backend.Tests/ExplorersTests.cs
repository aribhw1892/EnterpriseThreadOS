using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Documents;
using ETOS.Backend.Explorers;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class ExplorersTests
{
    [Fact]
    public async Task ContextView_artifact_returns_expected_sections()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedArtifactWithDependencies(dbContext);
        var service = CreateContextViewService(dbContext, context);

        var view = await service.GetContextViewAsync(
            ContextViewAnchorKind.Artifact,
            context.ArtifactId.ToString(),
            "published-policy",
            CancellationToken.None);

        Assert.Contains(view.Sections, section => section.SectionKey == "overview" && section.Visibility == ContextViewSectionVisibility.Visible);
        Assert.Contains(view.Sections, section => section.SectionKey == "relationships" && section.Items.Count > 0);
        Assert.Contains(view.Sections, section => section.SectionKey == "versions" && section.Items.Count > 0);
        Assert.Contains(view.Sections, section => section.SectionKey == "dependencies" && section.Items.Count > 0);
    }

    [Fact]
    public async Task ContextView_omits_sections_without_domain_permission()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedArtifactWithDependencies(dbContext);
        var service = CreateContextViewService(
            dbContext,
            context,
            permissionService: new SelectivePermissionService(
                ExplorerPermissions.ContextView,
                ArtifactPermissions.Read));

        var view = await service.GetContextViewAsync(
            ContextViewAnchorKind.Artifact,
            context.ArtifactId.ToString(),
            null,
            CancellationToken.None);

        var traceSection = view.Sections.Single(section => section.SectionKey == "ai-trace");
        Assert.Equal(ContextViewSectionVisibility.Denied, traceSection.Visibility);
        Assert.Empty(traceSection.Items);
    }

    [Fact]
    public async Task ContextView_graph_node_uses_governed_query_context()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var service = CreateContextViewService(dbContext, context, new RecordingGraphMemoryService(context.GraphNodeId));

        var view = await service.GetContextViewAsync(
            ContextViewAnchorKind.GraphNode,
            context.GraphNodeId.ToString(),
            "published-policy",
            CancellationToken.None);

        var evidence = view.Sections.Single(section => section.SectionKey == "evidence");
        Assert.Equal(ContextViewSectionVisibility.Visible, evidence.Visibility);
        Assert.Contains(evidence.Items, item => item.ItemType == "part");
        Assert.DoesNotContain(evidence.Items, item => item.SafeSummary.Contains("Secret cost rollup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ContextView_enforces_tenant_isolation()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedArtifactWithDependencies(dbContext);
        var service = CreateContextViewService(dbContext, context with { TenantId = Guid.NewGuid() });

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            service.GetContextViewAsync(
                ContextViewAnchorKind.Artifact,
                context.ArtifactId.ToString(),
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task GraphExplorer_filters_untrusted_nodes_by_default()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new FilteringGraphMemoryService(context.GraphNodeId);
        var service = CreateGraphExplorerService(dbContext, context, graph);

        var nodes = await service.ListNodesAsync(null, null, null, null, "published-policy", CancellationToken.None);

        Assert.Contains(nodes, node => node.NodeId == context.GraphNodeId);
        Assert.DoesNotContain(nodes, node => node.TrustState == TrustState.Provisional.ToString());
    }

    [Fact]
    public async Task GraphExplorer_policy_denies_restricted_summaries()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var graph = new FilteringGraphMemoryService(context.GraphNodeId);
        var service = CreateGraphExplorerService(dbContext, context, graph, new FilteringPolicyService());

        var nodes = await service.ListNodesAsync(null, null, null, 10, "published-policy", CancellationToken.None);
        var secretNode = nodes.Single(node =>
            node.AllowedAttributes.Count == 0
            && node.SafeSummary.Contains("Secret", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(secretNode.AllowedAttributes);
    }

    [Fact]
    public async Task GovernanceFlow_includes_dependencies_and_trace_links()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedArtifactWithTraceLink(dbContext);
        var service = CreateGovernanceFlowService(dbContext, context);

        var flow = await service.BuildFlowAsync(
            ContextViewAnchorKind.Artifact,
            context.ArtifactId.ToString(),
            CancellationToken.None);

        Assert.Contains(flow.Edges, edge => edge.Kind == GovernanceFlowEdgeKind.Dependency);
        Assert.Contains(flow.Edges, edge => edge.Kind == GovernanceFlowEdgeKind.TraceLink);
    }

    [Fact]
    public async Task GovernanceFlow_includes_review_chain_placeholders()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedArtifactWithDependencies(dbContext);
        var service = CreateGovernanceFlowService(dbContext, context);

        var flow = await service.BuildFlowAsync(
            ContextViewAnchorKind.Artifact,
            context.ArtifactId.ToString(),
            CancellationToken.None);

        Assert.Equal(5, flow.FutureChainPlaceholders.Count);
        Assert.All(flow.FutureChainPlaceholders, placeholder => Assert.Equal("not_implemented", placeholder.Status));
    }

    [Fact]
    public async Task ContextPackageExplorer_lists_runs_with_package_ids()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var governedQuery = CreateGovernedQueryService(dbContext, context, new RecordingGraphMemoryService(context.GraphNodeId));
        await governedQuery.RunAsync(
            new RunGovernedQueryRequest("object-360-context", context.GraphNodeId, null, "published-policy", null, 2),
            CancellationToken.None);
        var service = CreateContextPackageExplorerService(dbContext, context, governedQuery);

        var packages = await service.ListPackagesAsync(CancellationToken.None);

        Assert.NotEmpty(packages);
        Assert.All(packages, package => Assert.NotEqual(Guid.Empty, package.PackageId));
    }

    [Fact]
    public async Task DecisionExplorer_returns_decision_artifact_types_only()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedDecisionArtifacts(dbContext);
        var service = CreateDecisionExplorerService(dbContext, context);

        var decisions = await service.ListDecisionsAsync(null, null, null, CancellationToken.None);

        Assert.Single(decisions);
        Assert.Equal("decision", decisions.First().ArtifactType, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dependency_projection_matches_registry_impact()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedArtifactWithDependencies(dbContext);
        var registry = CreateArtifactRegistryService(dbContext, context);
        var contextView = CreateContextViewService(dbContext, context);

        var view = await contextView.GetContextViewAsync(
            ContextViewAnchorKind.Artifact,
            context.ArtifactId.ToString(),
            null,
            CancellationToken.None);
        var impact = await registry.GetImpactAsync(context.ArtifactId, context.VersionId, CancellationToken.None);
        var dependencySection = view.Sections.Single(section => section.SectionKey == "dependencies");

        Assert.Equal(impact.Dependencies.Count + impact.Dependents.Count, dependencySection.Items.Count);
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
        var context = new TestContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            Guid.Empty);

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
        dbContext.SaveChanges();
        return context;
    }

    private static TestContext SeedArtifactWithDependencies(EnterpriseThreadDbContext dbContext)
    {
        var context = SeedTenantUserAndDocument(dbContext);
        var requiredArtifactId = Guid.NewGuid();
        dbContext.Artifacts.Add(new Artifact
        {
            Id = requiredArtifactId,
            TenantId = context.TenantId,
            ArtifactType = "model-package",
            NormalizedArtifactType = "MODEL-PACKAGE",
            Name = "Required package",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = context.VersionId,
            TenantId = context.TenantId,
            ArtifactId = context.ArtifactId,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            ReadinessState = ArtifactReadinessState.Ready,
            CompatibilityStatus = ArtifactCompatibilityStatus.Compatible,
            PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        var requiredVersionId = Guid.NewGuid();
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = requiredVersionId,
            TenantId = context.TenantId,
            ArtifactId = requiredArtifactId,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            ReadinessState = ArtifactReadinessState.Ready,
            CompatibilityStatus = ArtifactCompatibilityStatus.Compatible,
            PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactRelationships.Add(new ArtifactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceArtifactId = context.ArtifactId,
            TargetArtifactId = requiredArtifactId,
            RelationshipType = ArtifactRelationshipType.RelatedTo,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactDependencies.Add(new ArtifactDependency
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DependentVersionId = context.VersionId,
            RequiredArtifactId = requiredArtifactId,
            RequiredVersionId = requiredVersionId,
            DependencyKind = ArtifactDependencyKind.DependsOn,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context with { RequiredArtifactId = requiredArtifactId, RequiredVersionId = requiredVersionId };
    }

    private static TestContext SeedArtifactWithTraceLink(EnterpriseThreadDbContext dbContext)
    {
        var context = SeedArtifactWithDependencies(dbContext);
        var runId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var traceId = Guid.NewGuid();
        dbContext.RetrievalRuns.Add(new RetrievalRun
        {
            Id = runId,
            TenantId = context.TenantId,
            QueryIntentVersionId = Guid.NewGuid(),
            RetrievalStrategyVersionId = Guid.NewGuid(),
            QueryText = "trace",
            Status = "Completed",
            SafeSummary = "Trace run",
            RequestedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });
        dbContext.ContextPackages.Add(new ContextPackage
        {
            Id = packageId,
            TenantId = context.TenantId,
            RetrievalRunId = runId,
            RetrievedContextJson = "[]",
            FilteredContextJson = "[]",
            DeniedSummariesJson = "[]",
            SensitiveDeniedReferencesJson = "[]",
            LlmVisibleContextJson = "[]",
            SafeSummary = "Package",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.AiTraceRecords.Add(new AiTraceRecord
        {
            Id = traceId,
            TenantId = context.TenantId,
            RetrievalRunId = runId,
            ContextPackageId = packageId,
            IntentKey = "object-360-context",
            StrategyKey = "default",
            QueryText = "trace",
            Status = "Completed",
            SafeSummary = "Trace",
            SourcesSummaryJson = "[]",
            FilteredSummariesJson = "[]",
            DeniedSafeSummariesJson = "[]",
            SensitiveDeniedReferencesJson = "[]",
            ConfidenceImpactJson = "{}",
            RequestedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.AiTraceArtifactLinks.Add(new AiTraceArtifactLink
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            AiTraceRecordId = traceId,
            LinkKind = AiTraceArtifactLinkKind.DraftArtifact,
            ObjectType = nameof(Artifact),
            ObjectId = context.ArtifactId.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private static TestContext SeedDecisionArtifacts(EnterpriseThreadDbContext dbContext)
    {
        var context = SeedTenantUserAndDocument(dbContext);
        dbContext.Artifacts.Add(new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = "dashboard",
            NormalizedArtifactType = "DASHBOARD",
            Name = "Not a decision",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var decisionArtifactId = Guid.NewGuid();
        dbContext.Artifacts.Add(new Artifact
        {
            Id = decisionArtifactId,
            TenantId = context.TenantId,
            ArtifactType = "decision",
            NormalizedArtifactType = "DECISION",
            Name = "Approve pump change",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = decisionArtifactId,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            PayloadJson = """{"title":"Approve pump change","status":"draft","participantUserIds":["11111111-1111-1111-1111-111111111111"],"evidenceCount":2,"conflictState":"none","outcomeSummary":"Pending review"}""",
            ReadinessState = ArtifactReadinessState.Draft,
            CompatibilityStatus = ArtifactCompatibilityStatus.Compatible,
            PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private static ContextViewService CreateContextViewService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGraphMemoryService? graphMemoryService = null,
        IAccessPermissionService? permissionService = null)
    {
        var graph = graphMemoryService ?? new RecordingGraphMemoryService(context.GraphNodeId);
        var governedQuery = CreateGovernedQueryService(dbContext, context, graph);
        return new ContextViewService(
            dbContext,
            new StaticTenantContextResolver(context),
            permissionService ?? new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            CreateArtifactRegistryService(dbContext, context),
            CreateDocumentService(dbContext, context, graph),
            governedQuery,
            CreateAiTraceService(dbContext, context),
            CreateGovernanceFlowService(dbContext, context));
    }

    private static GovernanceFlowService CreateGovernanceFlowService(
        EnterpriseThreadDbContext dbContext,
        TestContext context)
    {
        return new GovernanceFlowService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            CreateArtifactRegistryService(dbContext, context),
            CreateAiTraceService(dbContext, context));
    }

    private static GraphExplorerService CreateGraphExplorerService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGraphMemoryService graphMemoryService,
        IClassificationPolicyService? policyService = null)
    {
        return new GraphExplorerService(
            graphMemoryService,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new ExplorerPolicyFilter(policyService ?? new AllowAllPolicyService()));
    }

    private static ContextPackageExplorerService CreateContextPackageExplorerService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGovernedQueryService governedQueryService)
    {
        return new ContextPackageExplorerService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            governedQueryService);
    }

    private static DecisionExplorerFoundationService CreateDecisionExplorerService(
        EnterpriseThreadDbContext dbContext,
        TestContext context)
    {
        return new DecisionExplorerFoundationService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder());
    }

    private static GovernedQueryService CreateGovernedQueryService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGraphMemoryService graphMemoryService)
    {
        return new GovernedQueryService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            graphMemoryService,
            new AllowAllPolicyService(),
            new NoOpAiTraceRecorder());
    }

    private static ArtifactRegistryService CreateArtifactRegistryService(
        EnterpriseThreadDbContext dbContext,
        TestContext context)
    {
        return new ArtifactRegistryService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new AllowAllPolicyService());
    }

    private static DocumentService CreateDocumentService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGraphMemoryService graphMemoryService)
    {
        return new DocumentService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new NoOpDocumentFileStorage(),
            new NoOpDocumentVectorIndexingService(),
            new NoOpCadParsingPlaceholder(),
            graphMemoryService,
            new AllowAllPolicyService());
    }

    private static AiTraceService CreateAiTraceService(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        return new AiTraceService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder());
    }

    private sealed record TestContext(
        Guid TenantId,
        Guid UserId,
        Guid GraphNodeId,
        Guid ArtifactId,
        Guid DocumentArtifactId,
        Guid VersionId,
        Guid RequiredArtifactId,
        Guid RequiredVersionId);

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

    private sealed class SelectivePermissionService(params string[] allowedPermissions) : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(allowedPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase));
        }
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
            => throw new NotSupportedException();
    }

    private sealed class NoOpAiTraceRecorder : IAiTraceRecorder
    {
        public Task<Guid> CreateFromRetrievalRunAsync(Guid retrievalRunId, Guid? auditRecordId, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> CreateFromChatTurnAsync(Guid chatTurnId, Guid? auditRecordId, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid());
    }

    private sealed class NoOpDocumentFileStorage : IDocumentFileStorage
    {
        public Task<StoredDocumentFile> StoreAsync(Guid tenantId, Guid documentId, string originalFileName, Stream content, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class NoOpDocumentVectorIndexingService : IDocumentVectorIndexingService
    {
        public Task<DocumentVectorIndexStatus> RequestIndexAsync(DocumentVersion documentVersion, CancellationToken cancellationToken)
            => Task.FromResult(DocumentVectorIndexStatus.DisabledPlaceholder);
    }

    private sealed class NoOpCadParsingPlaceholder : ICadParsingPlaceholder
    {
        public CadParsingPlaceholderResponse GetStatus()
            => new(false, "Disabled", "CAD parsing is disabled in tests.");
    }

    private sealed class RecordingGraphMemoryService(Guid startNodeId) : IGraphMemoryService
    {
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
            return Task.FromResult(new GraphTraversalResult(start, [start], []));
        }

        public Task<GraphReadModel> ListGraphAsync(Guid tenantId, GraphSpace? graphSpace, string? sourceBatchId, IReadOnlyCollection<Guid>? nodeIds, IReadOnlyCollection<Guid>? relationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphReadModel([], []));

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(Guid tenantId, IReadOnlyCollection<Guid> stagingNodeIds, IReadOnlyCollection<Guid> stagingRelationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphPromotionCopyResult([], []));
    }

    private sealed class FilteringGraphMemoryService(Guid trustedNodeId) : IGraphMemoryService
    {
        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GraphReadModel> ListGraphAsync(Guid tenantId, GraphSpace? graphSpace, string? sourceBatchId, IReadOnlyCollection<Guid>? nodeIds, IReadOnlyCollection<Guid>? relationshipIds, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var trusted = new BaseNode(
                trustedNodeId,
                tenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "Trusted pump part." },
                null,
                now,
                now);
            var provisional = new BaseNode(
                Guid.NewGuid(),
                tenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Provisional,
                new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "Provisional supplier match." },
                null,
                now,
                now);
            var secret = new BaseNode(
                Guid.NewGuid(),
                tenantId,
                GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?> { ["classificationKey"] = "secret", ["safeSummary"] = "Secret cost rollup." },
                null,
                now,
                now);
            return Task.FromResult(new GraphReadModel([trusted, provisional, secret], []));
        }

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

            return Task.FromResult(new PolicyEvaluationResponse(Guid.NewGuid(), Guid.NewGuid(), request.PolicyKey, "v1", allowed, denied, []));
        }
    }
}
