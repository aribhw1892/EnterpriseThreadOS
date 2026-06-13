using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Documents;
using ETOS.Backend.Explorers;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedChat.Llm;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class RecommendationTests
{
    [Fact]
    public async Task ManualCreateWithEvidenceListsAndParses()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var service = CreateRecommendationService(dbContext, context);
        var traceId = Guid.NewGuid();

        var created = await service.CreateAsync(
            new CreateRecommendationRequest(
                "Review pump linkage",
                "Manual recommendation for pump evidence review.",
                RecommendationType.DocumentLink,
                RecommendationCreationSource.Manual,
                RecommendationRiskState.Medium,
                RecommendationCapabilityState.ReadOnlyAnalysis,
                [
                    new CreateRecommendationEvidenceLinkRequest(
                        EvidenceLinkType.ManualNote,
                        traceId,
                        "Engineer observed missing pump linkage in assembly review.",
                        TrustState.Trusted,
                        false)
                ],
                [
                    new CreateRecommendationSuggestedActionRequest(
                        "Review pump linkage",
                        "REVIEW_PUMP_LINK",
                        RecommendationRiskState.Medium,
                        "ENGINEERING_REVIEW",
                        null)
                ],
                null,
                null,
                true),
            CancellationToken.None);

        var list = await service.ListAsync(CancellationToken.None);
        Assert.Contains(list, item => item.Id == created.ArtifactId);

        var payload = await service.GetAsync(created.ArtifactId, created.VersionId, CancellationToken.None);
        Assert.Equal(RecommendationType.DocumentLink, payload.RecommendationType);
        Assert.Single(payload.EvidenceLinks);
        Assert.Single(payload.SuggestedActions);
    }

    [Fact]
    public async Task MarkReviewedRejectedWithZeroEvidence()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var version = SeedRecommendationVersion(dbContext, context, includeEvidence: false);
        var service = CreateRecommendationService(dbContext, context);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.MarkReviewedAsync(
            version.ArtifactId,
            version.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task MarkReviewedSucceedsWithManualNoteEvidence()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var version = SeedRecommendationVersion(dbContext, context, includeEvidence: true);
        var service = CreateRecommendationService(dbContext, context);

        var reviewed = await service.MarkReviewedAsync(version.ArtifactId, version.Id, CancellationToken.None);
        Assert.Equal(RecommendationLifecycleStatus.Reviewed, reviewed.LifecycleStatus);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenEvidenceConflicted()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var version = SeedRecommendationVersion(
            dbContext,
            context,
            includeEvidence: true,
            evidenceTrustState: TrustState.Conflicted,
            lifecycleStatus: RecommendationLifecycleStatus.Reviewed);
        var service = CreateRecommendationService(dbContext, context);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.MarkReadyAsync(
            version.ArtifactId,
            version.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task MarkReadyBlockedWhenDataQualitySourceExcludedFromTrustedRecommendations()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var issueId = SeedExcludedDataQualityIssue(dbContext, context);
        var factory = CreateRecommendationFactory(dbContext, context);
        var service = CreateRecommendationService(dbContext, context);

        var created = await factory.FromDataQualityIssueAsync(issueId, CancellationToken.None);
        await service.MarkReviewedAsync(created.ArtifactId, created.VersionId, CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.MarkReadyAsync(
            created.ArtifactId,
            created.VersionId,
            CancellationToken.None));
    }

    [Fact]
    public async Task MultipleSuggestedActionsValidateIndependently()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var service = CreateRecommendationService(dbContext, context);

        var created = await service.CreateAsync(
            new CreateRecommendationRequest(
                "Multi-action recommendation",
                "Recommendation with two suggested actions.",
                RecommendationType.BomSync,
                RecommendationCreationSource.Manual,
                RecommendationRiskState.High,
                RecommendationCapabilityState.ReviewRequired,
                [CreateManualEvidence()],
                [
                    new CreateRecommendationSuggestedActionRequest("Review EBOM", "REVIEW_EBOM", RecommendationRiskState.High, "ENGINEERING_REVIEW", null),
                    new CreateRecommendationSuggestedActionRequest("Review manufacturing impact", "REVIEW_MFG", RecommendationRiskState.Medium, "MANUFACTURING_REVIEW", null)
                ],
                null,
                null,
                true),
            CancellationToken.None);

        var payload = await service.GetAsync(created.ArtifactId, created.VersionId, CancellationToken.None);
        Assert.Equal(2, payload.SuggestedActions.Count);
        Assert.Equal("REVIEW_EBOM", payload.SuggestedActions.ElementAt(0).Kind);
        Assert.Equal("REVIEW_MFG", payload.SuggestedActions.ElementAt(1).Kind);
    }

    [Fact]
    public async Task InvalidSuggestedActionMissingKindFailsOnCreate()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var service = CreateRecommendationService(dbContext, context);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(
            new CreateRecommendationRequest(
                "Invalid action recommendation",
                "Should fail validation.",
                RecommendationType.DataQuality,
                RecommendationCreationSource.Manual,
                RecommendationRiskState.Low,
                RecommendationCapabilityState.ReadOnlyAnalysis,
                [CreateManualEvidence()],
                [new CreateRecommendationSuggestedActionRequest("Missing kind action", "", RecommendationRiskState.Low, null, null)],
                null,
                null,
                true),
            CancellationToken.None));
    }

    [Fact]
    public async Task FromDataQualityIssueLinksIssueAndSetsType()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var issueId = SeedDataQualityIssue(dbContext, context, excluded: false);
        var factory = CreateRecommendationFactory(dbContext, context);

        var created = await factory.FromDataQualityIssueAsync(issueId, CancellationToken.None);
        var payload = await CreateRecommendationService(dbContext, context)
            .GetAsync(created.ArtifactId, created.VersionId, CancellationToken.None);

        Assert.Equal(RecommendationType.DataQuality, payload.RecommendationType);
        Assert.Contains(payload.EvidenceLinks, link => link.EvidenceType == EvidenceLinkType.DataQualityIssue && link.SourceId == issueId);
    }

    [Fact]
    public async Task FromBomComparisonRunCreatesBomSyncRecommendation()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var runId = SeedBomComparisonRun(dbContext, context, missingInEbom: 2);
        var factory = CreateRecommendationFactory(dbContext, context);

        var created = await factory.FromBomComparisonRunAsync(runId, CancellationToken.None);
        var payload = await CreateRecommendationService(dbContext, context)
            .GetAsync(created.ArtifactId, created.VersionId, CancellationToken.None);

        Assert.Equal(RecommendationType.BomSync, payload.RecommendationType);
        Assert.Contains(payload.EvidenceLinks, link => link.EvidenceType == EvidenceLinkType.BomComparisonRun && link.SourceId == runId);
    }

    [Fact]
    public async Task ChatDraftRecommendationIncludesTraceExplainabilityRefs()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUserAndDocument(dbContext);
        var chatService = CreateChatService(dbContext, context);

        var session = await chatService.CreateSessionAsync(
            new CreateGovernedChatSessionRequest("Recommendation chat", context.GraphNodeId, null),
            CancellationToken.None);
        var turn = await chatService.AskAsync(
            session.Id,
            new CreateGovernedChatTurnRequest(
                "Draft a recommendation from BOM drift.",
                "bom-impact-context",
                context.GraphNodeId,
                null,
                "published-policy",
                ChatDraftArtifactKind.Recommendation),
            CancellationToken.None);

        Assert.NotNull(turn.DraftArtifact);
        Assert.Equal(RecommendationArtifactTypes.Recommendation, turn.DraftArtifact!.ArtifactType);

        var version = await dbContext.ArtifactVersions.SingleAsync(item => item.Id == turn.DraftArtifact.VersionId);
        var payload = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        Assert.Equal(RecommendationCreationSource.Chat, payload.CreationSource);
        Assert.NotNull(payload.Explainability?.AiTraceId);
        Assert.Contains(payload.EvidenceLinks, link => link.EvidenceType == EvidenceLinkType.AiTrace);
    }

    [Fact]
    public async Task CrossTenantGetDenied()
    {
        await using var dbContext = CreateDbContext();
        var owner = SeedTenantUser(dbContext);
        var version = SeedRecommendationVersion(dbContext, owner, includeEvidence: true);
        var otherTenantResolver = new StaticTenantContextResolver(owner with { TenantId = Guid.NewGuid() });
        var service = CreateRecommendationService(dbContext, owner, otherTenantResolver);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.GetAsync(
            version.ArtifactId,
            version.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task SuggestedActionStatusChangeWritesAuditRecord()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var version = SeedRecommendationVersion(dbContext, context, includeEvidence: true);
        var auditRecorder = new RecordingAuditRecorder();
        var service = CreateRecommendationService(dbContext, context, auditRecorder: auditRecorder);
        var payload = RecommendationPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var actionId = payload.SuggestedActions[0].ActionId;

        await service.UpdateSuggestedActionStatusAsync(
            version.ArtifactId,
            version.Id,
            actionId,
            new UpdateSuggestedActionStatusRequest(SuggestedActionStatus.SelectedForReview),
            CancellationToken.None);

        Assert.Contains(auditRecorder.Records, record => record.Action == "recommendations.suggested_action.update");
    }

    [Fact]
    public async Task GovernanceFlowForRecommendationArtifactOmitsNotImplementedPlaceholder()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantUser(dbContext);
        var version = SeedRecommendationVersion(dbContext, context, includeEvidence: true);
        var flowService = CreateGovernanceFlowService(dbContext, context);

        var flow = await flowService.BuildFlowAsync(
            ContextViewAnchorKind.Artifact,
            version.ArtifactId.ToString(),
            CancellationToken.None);

        Assert.Contains(flow.Nodes, node => node.LinkRoute == $"/recommendations/{version.ArtifactId}");
        Assert.DoesNotContain(flow.FutureChainPlaceholders, placeholder =>
            placeholder.Kind == GovernanceFlowPlaceholderKind.Recommendation
            && placeholder.Status == "not_implemented");
    }

    private static CreateRecommendationEvidenceLinkRequest CreateManualEvidence()
        => new(EvidenceLinkType.ManualNote, Guid.NewGuid(), "Manual evidence note.", TrustState.Trusted, false);

    private static RecommendationService CreateRecommendationService(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        ITenantContextResolver? tenantContextResolver = null,
        IAccessPermissionService? permissionService = null,
        RecordingAuditRecorder? auditRecorder = null)
    {
        return new RecommendationService(
            dbContext,
            tenantContextResolver ?? new StaticTenantContextResolver(context),
            permissionService ?? new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            auditRecorder ?? new RecordingAuditRecorder(),
            new AllowAllPolicyService(),
            new RecommendationEvidenceResolver(dbContext));
    }

    private static RecommendationFactory CreateRecommendationFactory(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        ITenantContextResolver? tenantContextResolver = null,
        IAccessPermissionService? permissionService = null)
    {
        return new RecommendationFactory(
            dbContext,
            tenantContextResolver ?? new StaticTenantContextResolver(context),
            permissionService ?? new AllowAllPermissionService(),
            new RecordingDenialRecorder());
    }

    private static GovernedChatService CreateChatService(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        var graphMemory = new RecordingGraphMemoryService(context.GraphNodeId);
        var governedQueryService = new GovernedQueryService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            graphMemory,
            new AllowAllPolicyService(),
            new AiTraceRecorder(dbContext));

        return new GovernedChatService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            governedQueryService,
            new GovernedChatArtifactSeeder(dbContext),
            new DeterministicLlmCompletionService(),
            new OutputSchemaValidator(),
            new ChatArtifactDraftBuilder(dbContext),
            new AiTraceRecorder(dbContext));
    }

    private static GovernanceFlowService CreateGovernanceFlowService(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        return new GovernanceFlowService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new ArtifactRegistryService(
                dbContext,
                new StaticTenantContextResolver(context),
                new AllowAllPermissionService(),
                new RecordingDenialRecorder(),
                new RecordingAuditRecorder(),
                new AllowAllPolicyService()),
            new AiTraceService(
                dbContext,
                new StaticTenantContextResolver(context),
                new AllowAllPermissionService(),
                new RecordingDenialRecorder(),
                new RecordingAuditRecorder()));
    }

    private static ArtifactVersion SeedRecommendationVersion(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        bool includeEvidence,
        TrustState evidenceTrustState = TrustState.Trusted,
        RecommendationLifecycleStatus lifecycleStatus = RecommendationLifecycleStatus.Draft)
    {
        var artifactId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var evidence = includeEvidence
            ? new[]
            {
                new RecommendationPayloadParser.RecommendationEvidenceLinkDocument(
                    Guid.NewGuid(),
                    EvidenceLinkType.ManualNote,
                    Guid.NewGuid(),
                    "Seed evidence summary.",
                    evidenceTrustState,
                    false)
            }
            : Array.Empty<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>();

        var payload = RecommendationPayloadParser.CreateDefault(
            "Seed recommendation",
            "Seed recommendation summary.",
            RecommendationType.BomSync,
            RecommendationCreationSource.Manual,
            RecommendationRiskState.Medium,
            RecommendationCapabilityState.ReadOnlyAnalysis,
            evidence,
            [
                new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                    Guid.NewGuid(),
                    "Review EBOM",
                    "REVIEW_EBOM",
                    RecommendationRiskState.Medium,
                    "ENGINEERING_REVIEW",
                    SuggestedActionStatus.Proposed,
                    null)
            ],
            [],
            null,
            true,
            null,
            null);
        payload.LifecycleStatus = lifecycleStatus;

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = RecommendationArtifactTypes.Recommendation,
            NormalizedArtifactType = RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            Name = "Seed recommendation",
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
            VersionLabel = "rec-seed-v1",
            NormalizedVersionLabel = "REC-SEED-V1",
            Summary = "Seed recommendation summary.",
            PayloadJson = RecommendationPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
        return version;
    }

    private static Guid SeedDataQualityIssue(EnterpriseThreadDbContext dbContext, TestContext context, bool excluded)
    {
        var issueId = Guid.NewGuid();
        dbContext.DataQualityIssues.Add(new DataQualityIssue
        {
            Id = issueId,
            TenantId = context.TenantId,
            Title = "Missing supplier link",
            IssueCode = "MISSING_SUPPLIER",
            NormalizedIssueCode = "MISSING_SUPPLIER",
            Severity = DataQualitySeverity.High,
            Origin = DataQualityIssueOrigin.ImportValidation,
            AffectedEntityType = DataQualityAffectedEntityType.GraphNode,
            GraphNodeId = context.GraphNodeId,
            ResultingTrustState = TrustState.Provisional,
            ExcludedFromTrustedRecommendations = excluded,
            EvidenceSummary = "Supplier link missing on imported part.",
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return issueId;
    }

    private static Guid SeedExcludedDataQualityIssue(EnterpriseThreadDbContext dbContext, TestContext context)
        => SeedDataQualityIssue(dbContext, context, excluded: true);

    private static Guid SeedBomComparisonRun(EnterpriseThreadDbContext dbContext, TestContext context, int missingInEbom)
    {
        var batchId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        dbContext.ImportBatches.Add(new ImportBatch
        {
            Id = batchId,
            TenantId = context.TenantId,
            SourceSystem = "demo-plm",
            NormalizedSourceSystem = "DEMO-PLM",
            Status = ImportBatchStatus.Staged,
            ActiveModelPackageVersionId = Guid.NewGuid(),
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.BomComparisonRuns.Add(new BomComparisonRun
        {
            Id = runId,
            TenantId = context.TenantId,
            ImportBatchId = batchId,
            SourceContext = "demo-plm",
            CadSummaryJson = "{}",
            EbomSummaryJson = "{}",
            MissingInCadCount = 0,
            MissingInEbomCount = missingInEbom,
            QuantityMismatchCount = 1,
            UsageReferenceMismatchCount = 0,
            UnresolvedIdentityCount = 0,
            ResultJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return runId;
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantUser(EnterpriseThreadDbContext dbContext)
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
        dbContext.SaveChanges();
        return context;
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
            => Task.FromResult(new SecurityEventResponse(
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

    private class AllowAllPolicyService : IClassificationPolicyService
    {
        public virtual Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new PolicyEvaluationResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request.PolicyKey,
                "v1",
                request.Items.Select(item => new PolicyAllowedContextResponse(item.ContextId, item.ContextType, item.SafeSummary)).ToList(),
                [],
                []));

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
        public Task<ArtifactPolicyRiskStatus> EvaluateArtifactPolicyRiskAsync(Guid tenantId, Guid artifactVersionId, CancellationToken cancellationToken)
            => Task.FromResult(ArtifactPolicyRiskStatus.Acceptable);
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
            var start = new BaseNode(startNodeId, request.TenantId, GraphSpace.Trusted, "part", TrustState.Trusted, new Dictionary<string, string?> { ["safeSummary"] = "Trusted pump part." }, null, now, now);
            return Task.FromResult(new GraphTraversalResult(start, [start], []));
        }

        public Task<GraphReadModel> ListGraphAsync(Guid tenantId, GraphSpace? graphSpace, string? sourceBatchId, IReadOnlyCollection<Guid>? nodeIds, IReadOnlyCollection<Guid>? relationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphReadModel([], []));
        public Task<GraphPromotionCopyResult> PromoteStagingAsync(Guid tenantId, IReadOnlyCollection<Guid> stagingNodeIds, IReadOnlyCollection<Guid> stagingRelationshipIds, CancellationToken cancellationToken)
            => Task.FromResult(new GraphPromotionCopyResult([], []));
    }
}
