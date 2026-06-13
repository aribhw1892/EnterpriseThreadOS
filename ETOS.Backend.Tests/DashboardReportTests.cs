using System.Text.Json;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Dashboards;
using ETOS.Backend.Documents;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedChat.Llm;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class DashboardReportTests
{
    [Fact]
    public async Task ChatCreatedDashboardTemplatePreviewsViaGovernedQuery()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var service = CreateDashboardReportService(dbContext, context);

        var preview = await service.PreviewAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, "published-policy"),
            CancellationToken.None);

        Assert.Equal(DashboardReportArtifactTypes.Dashboard, preview.ArtifactType);
        Assert.Contains(preview.Blocks, block => block.Kind == DashboardReportBlockKinds.GovernedQuery && block.AllowedCount > 0);
    }

    [Fact]
    public async Task PreviewExcludesDeniedContextFromBlockSummaries()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var service = CreateDashboardReportService(dbContext, context, new FilteringPolicyService());

        var preview = await service.PreviewAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, "published-policy"),
            CancellationToken.None);

        var queryBlock = preview.Blocks.Single(block => block.Kind == DashboardReportBlockKinds.GovernedQuery);
        Assert.True(queryBlock.DeniedCount > 0);
        Assert.DoesNotContain("Secret cost rollup", queryBlock.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewRejectsUnsupportedQueryIntentRef()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var version = SeedInvalidDashboardVersion(dbContext, context);
        var service = CreateDashboardReportService(dbContext, context);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.PreviewAsync(
            DashboardReportArtifactTypes.Dashboard,
            version.ArtifactId,
            version.Id,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task UserWithoutExportPermissionCannotExport()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var service = CreateDashboardReportService(
            dbContext,
            context,
            permissionService: new FixedPermissionService([
                DashboardReportPermissions.Preview,
                DashboardReportPermissions.Readiness,
                ArtifactPermissions.Read,
                GovernedQueryPermissions.Run,
                GovernedQueryPermissions.Read
            ]));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.ExportAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task ExportWritesAuditRecordAndRedactionMetadata()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var auditRecorder = new RecordingAuditRecorder();
        var service = CreateDashboardReportService(
            dbContext,
            context,
            permissionService: new FixedPermissionService([
                DashboardReportPermissions.Preview,
                DashboardReportPermissions.Export,
                DashboardReportPermissions.Readiness,
                ArtifactPermissions.Read,
                GovernedQueryPermissions.Run,
                GovernedQueryPermissions.Read
            ]),
            auditRecorder: auditRecorder);

        var export = await service.ExportAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, "published-policy"),
            CancellationToken.None);

        Assert.NotEmpty(export.Content);
        Assert.Equal("permissionSafe", export.Metadata.EvidenceLevel);
        Assert.NotEmpty(export.Metadata.ExportHash);
        Assert.Contains(auditRecorder.Records, record => record.Action == "dashboards_reports.export" && record.Result == AuditResult.Export);
        Assert.Equal(1, await dbContext.DashboardReportExportRecords.CountAsync());
        var exportRecord = await dbContext.DashboardReportExportRecords.SingleAsync();
        Assert.Contains("permissionSafe", exportRecord.RedactionMetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadyFailsWhenObject360WidgetMissingAnchor()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var version = SeedDashboardVersionWithoutAnchor(dbContext, context);
        var service = CreateDashboardReportService(dbContext, context);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.MarkReadyAsync(
            DashboardReportArtifactTypes.Dashboard,
            version.ArtifactId,
            version.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task MarkReadyThenPublishSucceedsWhenDependenciesPublished()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var dashboardService = CreateDashboardReportService(dbContext, context);
        var artifactService = CreateArtifactRegistryService(dbContext, context);

        await dashboardService.MarkReadyAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            CancellationToken.None);

        var publish = await artifactService.PublishVersionAsync(
            draft.ArtifactId,
            draft.VersionId,
            new PublishArtifactVersionRequest("Publish dashboard"),
            CancellationToken.None);

        Assert.True(publish.Succeeded);
        Assert.Equal(ArtifactReadinessState.Published, publish.Version.ReadinessState);
    }

    [Fact]
    public async Task PublishBlockedWhileStillDraft()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var artifactService = CreateArtifactRegistryService(dbContext, context);

        var publish = await artifactService.PublishVersionAsync(
            draft.ArtifactId,
            draft.VersionId,
            new PublishArtifactVersionRequest("Attempt publish"),
            CancellationToken.None);

        Assert.False(publish.Succeeded);
        Assert.Contains(publish.BlockingReasons, reason => reason.Contains("ready", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DependencyImpactReflectsUnpublishedOutputSchemaDependency()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var artifactService = CreateArtifactRegistryService(dbContext, context);

        var impact = await artifactService.GetImpactAsync(draft.ArtifactId, draft.VersionId, CancellationToken.None);

        Assert.NotEmpty(impact.Dependencies);
        Assert.Contains(impact.Dependencies, dependency => dependency.RequiredArtifactName.Contains("schema", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task KpiPlaceholderBlockReturnsDeferredCatalogMetadata()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var service = CreateDashboardReportService(dbContext, context);

        var preview = await service.PreviewAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, null),
            CancellationToken.None);

        var kpiBlock = preview.Blocks.Single(block => block.Kind == DashboardReportBlockKinds.GovernanceKpiPlaceholder);
        Assert.Equal("deferred", kpiBlock.Status);
        Assert.Equal("open_reviews", kpiBlock.KpiKey);
        Assert.Contains("Milestone 4", kpiBlock.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("count", kpiBlock.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossTenantPreviewIsDenied()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var draft = await CreateDashboardDraftAsync(dbContext, context);
        var otherContext = context with { TenantId = Guid.NewGuid() };
        var service = CreateDashboardReportService(dbContext, otherContext);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.PreviewAsync(
            DashboardReportArtifactTypes.Dashboard,
            draft.ArtifactId,
            draft.VersionId,
            new DashboardReportPreviewRequest(context.GraphNodeId, null, null),
            CancellationToken.None));
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> CreateDashboardDraftAsync(
        EnterpriseThreadDbContext dbContext,
        TestContext context)
    {
        var chatService = CreateChatService(dbContext, context, new RecordingGraphMemoryService(context.GraphNodeId));
        var session = await chatService.CreateSessionAsync(
            new CreateGovernedChatSessionRequest("Dashboard session", context.GraphNodeId, null),
            CancellationToken.None);
        var turn = await chatService.AskAsync(
            session.Id,
            new CreateGovernedChatTurnRequest("Draft a dashboard.", null, null, null, "published-policy", ChatDraftArtifactKind.Dashboard),
            CancellationToken.None);

        Assert.NotNull(turn.DraftArtifact);
        return (turn.DraftArtifact!.ArtifactId, turn.DraftArtifact.VersionId);
    }

    private static ArtifactVersion SeedDashboardVersionWithoutAnchor(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        var artifactId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            name = "Anchorless dashboard",
            summary = "Missing anchor",
            createdFromChat = false,
            defaultAnchor = new { startGraphNodeId = (Guid?)null, documentArtifactId = (Guid?)null },
            widgets = new[]
            {
                new
                {
                    widgetId = "primary-context",
                    title = "Context",
                    kind = "governed_query",
                    queryIntentRef = "object-360-context",
                    visualization = "summary_list"
                }
            }
        });

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = DashboardReportArtifactTypes.Dashboard,
            NormalizedArtifactType = DashboardReportArtifactTypes.Dashboard.ToUpperInvariant(),
            Name = "Anchorless dashboard",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var version = new ArtifactVersion
        {
            Id = versionId,
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "draft-v1",
            NormalizedVersionLabel = "DRAFT-V1",
            Summary = "Draft",
            PayloadJson = payload,
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
        return version;
    }

    private static ArtifactVersion SeedInvalidDashboardVersion(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        var artifactId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            name = "Invalid dashboard",
            summary = "Bad intent",
            createdFromChat = false,
            defaultAnchor = new { startGraphNodeId = context.GraphNodeId, documentArtifactId = (Guid?)null },
            widgets = new[]
            {
                new
                {
                    widgetId = "bad-intent",
                    title = "Bad",
                    kind = "governed_query",
                    queryIntentRef = "tenant-custom-intent",
                    visualization = "summary_list"
                }
            }
        });

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = DashboardReportArtifactTypes.Dashboard,
            NormalizedArtifactType = DashboardReportArtifactTypes.Dashboard.ToUpperInvariant(),
            Name = "Invalid dashboard",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var version = new ArtifactVersion
        {
            Id = versionId,
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "draft-v1",
            NormalizedVersionLabel = "DRAFT-V1",
            Summary = "Draft",
            PayloadJson = payload,
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
        return version;
    }

    private static DashboardReportService CreateDashboardReportService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IClassificationPolicyService? policyService = null,
        IAccessPermissionService? permissionService = null,
        RecordingAuditRecorder? auditRecorder = null)
    {
        var permissions = permissionService ?? new AllowAllPermissionService();
        var audit = auditRecorder ?? new RecordingAuditRecorder();
        var governedQueryService = new GovernedQueryService(
            dbContext,
            new StaticTenantContextResolver(context),
            permissions,
            new RecordingDenialRecorder(),
            audit,
            new RecordingGraphMemoryService(context.GraphNodeId),
            policyService ?? new AllowAllPolicyService(),
            new AiTraceRecorder(dbContext));

        return new DashboardReportService(
            dbContext,
            new StaticTenantContextResolver(context),
            permissions,
            new RecordingDenialRecorder(),
            audit,
            governedQueryService,
            policyService ?? new AllowAllPolicyService());
    }

    private static GovernedChatService CreateChatService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        IGraphMemoryService graphMemoryService)
    {
        return new GovernedChatService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new GovernedQueryService(
                dbContext,
                new StaticTenantContextResolver(context),
                new AllowAllPermissionService(),
                new RecordingDenialRecorder(),
                new RecordingAuditRecorder(),
                graphMemoryService,
                new AllowAllPolicyService(),
                new AiTraceRecorder(dbContext)),
            new GovernedChatArtifactSeeder(dbContext),
            new DeterministicLlmCompletionService(),
            new OutputSchemaValidator(),
            new ChatArtifactDraftBuilder(dbContext),
            new AiTraceRecorder(dbContext));
    }

    private static ArtifactRegistryService CreateArtifactRegistryService(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        return new ArtifactRegistryService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new AllowAllPolicyService());
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

    private sealed class FixedPermissionService(IReadOnlyCollection<string> allowed) : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
            => Task.FromResult(allowed.Contains(permissionKey, StringComparer.OrdinalIgnoreCase));
    }

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingAuditRecorder : IAuditRecorder
    {
        public List<AuditRecordWriteRequest> Records { get; } = [];

        public Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
        {
            Records.Add(request);
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
            var start = new BaseNode(startNodeId, request.TenantId, GraphSpace.Trusted, "part", TrustState.Trusted, new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "Trusted pump part." }, null, now, now);
            var secret = new BaseNode(secretNodeId, request.TenantId, GraphSpace.Trusted, "part", TrustState.Trusted, new Dictionary<string, string?> { ["classificationKey"] = "secret", ["safeSummary"] = "Secret cost rollup." }, null, now, now);
            var provisional = new BaseNode(provisionalNodeId, request.TenantId, GraphSpace.Trusted, "part", TrustState.Provisional, new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "provisional supplier match." }, null, now, now);
            var relationship = new BaseRelationship(Guid.NewGuid(), request.TenantId, startNodeId, secretNodeId, "BOM_CHILD", TrustState.Trusted, new Dictionary<string, string?> { ["classificationKey"] = "internal", ["safeSummary"] = "Trusted BOM edge." }, null, now, now);
            return Task.FromResult(new GraphTraversalResult(start, [start, secret, provisional], [relationship]));
        }

        public Task<GraphReadModel> ListGraphAsync(Guid tenantId, GraphSpace? graphSpace, string? sourceBatchId, IReadOnlyCollection<Guid>? nodeIds, IReadOnlyCollection<Guid>? relationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphReadModel([], []));

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(Guid tenantId, IReadOnlyCollection<Guid> stagingNodeIds, IReadOnlyCollection<Guid> stagingRelationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphPromotionCopyResult([], []));
    }
}
