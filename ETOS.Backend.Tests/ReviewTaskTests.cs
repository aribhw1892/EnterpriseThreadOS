using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class ReviewTaskPriorityDeriverTests
{
    [Theory]
    [InlineData(RecommendationRiskState.Critical, TrustState.Conflicted, RecommendationConflictState.Blocked, ReviewTaskPriority.Critical)]
    [InlineData(RecommendationRiskState.High, TrustState.Provisional, RecommendationConflictState.Partial, ReviewTaskPriority.High)]
    [InlineData(RecommendationRiskState.Low, TrustState.Trusted, RecommendationConflictState.None, ReviewTaskPriority.Low)]
    public void DerivePriorityMatrix(
        RecommendationRiskState severity,
        TrustState trustState,
        RecommendationConflictState conflictState,
        ReviewTaskPriority expected)
    {
        var deriver = new ReviewTaskPriorityDeriver();
        var actual = deriver.Derive(severity, trustState, conflictState, null);
        Assert.Equal(expected, actual);
    }
}

public sealed class ReviewTaskTemplateTests
{
    [Fact]
    public void TemplateResolverMapsDataQualitySourceToTemplateKey()
    {
        var key = ReviewTaskTemplateResolver.ResolveTemplateKey(
            ReviewTaskSourceType.DataQuality,
            null,
            RecommendationType.DataQuality);
        Assert.Equal("data-quality-review", key);
    }

    [Fact]
    public void EscalationValidationRequiresRoleWhenEnabled()
    {
        Assert.Throws<RequestValidationException>(() =>
            ReviewTaskTemplatePayloadParser.ValidateEscalationPath(
                new ReviewTaskTemplatePayloadParser.ReviewTaskTemplateEscalationPathDocument
                {
                    Enabled = true
                }));
    }
}

public sealed class ReviewTaskTests
{
    [Fact]
    public async Task AssignNonTenantMemberForbidden()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        var outsiderId = Guid.NewGuid();
        dbContext.Users.Add(new EtosUser
        {
            Id = outsiderId,
            UserName = "outsider@example.test",
            NormalizedUserName = "OUTSIDER@EXAMPLE.TEST",
            Email = "outsider@example.test",
            NormalizedEmail = "OUTSIDER@EXAMPLE.TEST",
            DisplayName = "Outsider",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var created = await CreateReviewTaskFactory(dbContext, context).CreateManualAsync(
            new CreateReviewTaskRequest(
                "Manual review",
                "business-action-review",
                ReviewTaskSourceType.Manual,
                "manual-1",
                context.UserId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        var service = CreateReviewTaskService(dbContext, context);
        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.AssignAsync(
            created.ArtifactId,
            created.VersionId,
            new AssignReviewTaskRequest(outsiderId, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task FromRecommendationActionSetsConvertedStatusAndLinksEvidence()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "business-action-review", requiresDq: false);

        var actionId = Guid.NewGuid();
        var version = SeedRecommendationWithAction(dbContext, context, actionId, includeDataQualityEvidence: false);
        var factory = CreateReviewTaskFactory(dbContext, context);

        var created = await factory.FromRecommendationActionAsync(
            version.ArtifactId,
            version.Id,
            actionId,
            CancellationToken.None);

        var service = CreateReviewTaskService(dbContext, context);
        var payload = await service.GetAsync(created.ArtifactId, created.VersionId, CancellationToken.None);
        Assert.Equal(ReviewTaskSourceType.Recommendation, payload.Source.SourceType);
        Assert.NotEmpty(payload.EvidenceReferences);
        Assert.Equal(actionId, payload.SuggestedActionId);

        var recommendationVersion = await dbContext.ArtifactVersions.SingleAsync(item => item.Id == version.Id);
        var recommendation = RecommendationPayloadParser.Deserialize(recommendationVersion.PayloadJson!);
        Assert.Equal(SuggestedActionStatus.ConvertedToReviewTask, recommendation.SuggestedActions.Single(item => item.ActionId == actionId).Status);
    }

    [Fact]
    public async Task CompleteReturnsDecisionCreationDeferred()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "data-quality-review", requiresDq: false);

        var created = await CreateReviewTaskFactory(dbContext, context).FromDataQualityIssueAsync(
            SeedDataQualityIssue(dbContext, context),
            CancellationToken.None);

        var completed = await CreateReviewTaskService(dbContext, context).CompleteAsync(
            created.ArtifactId,
            created.VersionId,
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Accepted, "Accepted resolution."),
            CancellationToken.None);

        Assert.True(completed.DecisionCreationDeferred);
        Assert.Equal(ReviewTaskStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task EscalationWithoutEnabledPathFails()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "access-request-review", requiresDq: false, escalationEnabled: false);

        var created = await CreateReviewTaskFactory(dbContext, context).CreateManualAsync(
            new CreateReviewTaskRequest(
                "No escalation task",
                "access-request-review",
                ReviewTaskSourceType.Manual,
                "manual-2",
                context.UserId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(() => CreateReviewTaskService(dbContext, context).CreateEscalationTaskAsync(
            created.ArtifactId,
            created.VersionId,
            CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentPersistsAppendOnlyComment()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "data-quality-review", requiresDq: false);

        var created = await CreateReviewTaskFactory(dbContext, context).FromDataQualityIssueAsync(
            SeedDataQualityIssue(dbContext, context),
            CancellationToken.None);

        await CreateReviewTaskService(dbContext, context).AddCommentAsync(
            created.ArtifactId,
            created.VersionId,
            new AddReviewTaskCommentRequest("Needs engineering review."),
            CancellationToken.None);

        var payload = await CreateReviewTaskService(dbContext, context).GetAsync(
            created.ArtifactId,
            created.VersionId,
            CancellationToken.None);
        Assert.Single(payload.Comments);
        Assert.Equal("Needs engineering review.", payload.Comments.Single().Body);
    }

    private static ReviewTaskFactory CreateReviewTaskFactory(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new ReviewTaskTemplateResolver(dbContext),
            new ReviewTaskPriorityDeriver(),
            new ReviewTaskChainService(dbContext, new RecordingAuditRecorder()));

    private static ReviewTaskService CreateReviewTaskService(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new ReviewTaskChainService(dbContext, new RecordingAuditRecorder()),
            new DeferredReviewTaskCompletionHandler());

    private static async Task SeedPublishedTemplateAsync(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        string templateKey,
        bool requiresDq,
        bool escalationEnabled = true)
    {
        var payload = ReviewTaskTemplatePayloadParser.Create(
            templateKey,
            templateKey,
            null,
            requiresDq,
            new ReviewTaskTemplatePayloadParser.ReviewTaskTemplateEscalationPathDocument
            {
                Enabled = escalationEnabled,
                EscalationTargetRoleKey = escalationEnabled ? "tenant-admin" : null
            },
            null,
            ["accept", "reject"]);

        var artifactId = Guid.NewGuid();
        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate,
            NormalizedArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant(),
            Name = templateKey,
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = templateKey,
            PayloadJson = ReviewTaskTemplatePayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static ArtifactVersion SeedRecommendationWithAction(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        Guid actionId,
        bool includeDataQualityEvidence)
    {
        var artifactId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var issueId = includeDataQualityEvidence ? SeedDataQualityIssue(dbContext, context) : Guid.NewGuid();
        var evidence = includeDataQualityEvidence
            ? new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>
            {
                new(
                    Guid.NewGuid(),
                    EvidenceLinkType.DataQualityIssue,
                    issueId,
                    "Linked data quality issue.",
                    TrustState.Provisional,
                    false)
            }
            : new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>
            {
                new(
                    Guid.NewGuid(),
                    EvidenceLinkType.ManualNote,
                    Guid.NewGuid(),
                    "Manual evidence.",
                    TrustState.Trusted,
                    false)
            };

        var payload = RecommendationPayloadParser.CreateDefault(
            "Recommendation for review task",
            "Summary.",
            RecommendationType.BomSync,
            RecommendationCreationSource.Manual,
            RecommendationRiskState.High,
            RecommendationCapabilityState.ReviewRequired,
            evidence,
            [
                new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                    actionId,
                    "Review BOM change",
                    "REVIEW_BOM",
                    RecommendationRiskState.High,
                    "ENGINEERING_REVIEW",
                    SuggestedActionStatus.Proposed,
                    null)
            ],
            [],
            null,
            true,
            null,
            null);

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = RecommendationArtifactTypes.Recommendation,
            NormalizedArtifactType = RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            Name = "Recommendation",
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
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = "Summary.",
            PayloadJson = RecommendationPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
        return version;
    }

    private static Guid SeedDataQualityIssue(EnterpriseThreadDbContext dbContext, TestContext context)
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
            Origin = DataQualityIssueOrigin.Manual,
            AffectedEntityType = DataQualityAffectedEntityType.GraphNode,
            GraphNodeId = context.GraphNodeId,
            ResultingTrustState = TrustState.Provisional,
            EvidenceSummary = "Supplier link missing.",
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return issueId;
    }

    private static TestContext SeedTenantWithMembership(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        dbContext.Tenants.Add(new Tenant
        {
            Id = context.TenantId,
            Identifier = "demo",
            NormalizedIdentifier = "DEMO",
            Name = "Demo Tenant",
            CreatedAt = DateTimeOffset.UtcNow
        });
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
        var roleId = Guid.NewGuid();
        dbContext.TenantRoles.Add(new TenantRole
        {
            Id = roleId,
            TenantId = context.TenantId,
            Name = "Admin",
            NormalizedName = "ADMIN",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = context.UserId,
            TenantRoleId = roleId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
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

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingAuditRecorder : IAuditRecorder
    {
        public Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AuditRecordResponse(
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
}

public sealed class ReviewTaskChainTests
{
    [Fact]
    public async Task DataQualityPrerequisiteUnblocksBusinessTaskOnAcceptedCompletion()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "data-quality-review", requiresDq: false);
        await SeedPublishedTemplateAsync(dbContext, context, "business-action-review", requiresDq: true);

        var actionId = Guid.NewGuid();
        var version = SeedRecommendationWithAction(dbContext, context, actionId, includeDataQualityEvidence: true);
        var factory = CreateReviewTaskFactory(dbContext, context);
        var service = CreateReviewTaskService(dbContext, context);

        var businessTask = await factory.FromRecommendationActionAsync(
            version.ArtifactId,
            version.Id,
            actionId,
            CancellationToken.None);

        var businessPayload = await service.GetAsync(businessTask.ArtifactId, businessTask.VersionId, CancellationToken.None);
        Assert.Equal(ReviewTaskStatus.Blocked, businessPayload.Status);
        Assert.NotEmpty(businessPayload.PrerequisiteTaskIds);

        var dqTaskId = businessPayload.PrerequisiteTaskIds.Single();
        var dqVersion = await dbContext.ArtifactVersions
            .Where(item => item.ArtifactId == dqTaskId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstAsync();

        var completed = await service.CompleteAsync(
            dqTaskId,
            dqVersion.Id,
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Accepted, "DQ accepted."),
            CancellationToken.None);

        Assert.Contains(businessTask.ArtifactId, completed.UnblockedTaskArtifactIds);

        var updatedBusiness = await service.GetAsync(businessTask.ArtifactId, businessTask.VersionId, CancellationToken.None);
        Assert.Equal(ReviewTaskStatus.Open, updatedBusiness.Status);
    }

    private static async Task SeedPublishedTemplateAsync(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        string templateKey,
        bool requiresDq)
    {
        var payload = ReviewTaskTemplatePayloadParser.Create(templateKey, templateKey, null, requiresDq, null, null, ["accept"]);
        var artifactId = Guid.NewGuid();
        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate,
            NormalizedArtifactType = ReviewTaskTemplateArtifactTypes.ReviewTaskTemplate.ToUpperInvariant(),
            Name = templateKey,
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = ReviewTaskTemplatePayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static ArtifactVersion SeedRecommendationWithAction(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        Guid actionId,
        bool includeDataQualityEvidence)
    {
        var artifactId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        dbContext.DataQualityIssues.Add(new DataQualityIssue
        {
            Id = issueId,
            TenantId = context.TenantId,
            Title = "DQ issue",
            IssueCode = "DQ",
            NormalizedIssueCode = "DQ",
            Severity = DataQualitySeverity.High,
            Origin = DataQualityIssueOrigin.Manual,
            AffectedEntityType = DataQualityAffectedEntityType.GraphNode,
            GraphNodeId = context.GraphNodeId,
            ResultingTrustState = TrustState.Provisional,
            EvidenceSummary = "Evidence.",
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var evidence = includeDataQualityEvidence
            ? new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>
            {
                new(
                    Guid.NewGuid(),
                    EvidenceLinkType.DataQualityIssue,
                    issueId,
                    "Linked DQ issue.",
                    TrustState.Provisional,
                    false)
            }
            : new List<RecommendationPayloadParser.RecommendationEvidenceLinkDocument>();

        var payload = RecommendationPayloadParser.CreateDefault(
            "Rec",
            "Summary.",
            RecommendationType.BomSync,
            RecommendationCreationSource.Manual,
            RecommendationRiskState.High,
            RecommendationCapabilityState.ReviewRequired,
            evidence,
            [
                new RecommendationPayloadParser.RecommendationSuggestedActionDocument(
                    actionId,
                    "Review",
                    "REVIEW",
                    RecommendationRiskState.High,
                    "ENGINEERING_REVIEW",
                    SuggestedActionStatus.Proposed,
                    null)
            ],
            [],
            null,
            true,
            null,
            null);

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = RecommendationArtifactTypes.Recommendation,
            NormalizedArtifactType = RecommendationArtifactTypes.Recommendation.ToUpperInvariant(),
            Name = "Rec",
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
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            PayloadJson = RecommendationPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
        return version;
    }

    private static ReviewTaskFactory CreateReviewTaskFactory(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new ReviewTaskTemplateResolver(dbContext),
            new ReviewTaskPriorityDeriver(),
            new ReviewTaskChainService(dbContext, new RecordingAuditRecorder()));

    private static ReviewTaskService CreateReviewTaskService(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new RecordingAuditRecorder(),
            new ReviewTaskChainService(dbContext, new RecordingAuditRecorder()),
            new DeferredReviewTaskCompletionHandler());

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantWithMembership(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        dbContext.Tenants.Add(new Tenant
        {
            Id = context.TenantId,
            Identifier = "demo",
            NormalizedIdentifier = "DEMO",
            Name = "Demo Tenant",
            CreatedAt = DateTimeOffset.UtcNow
        });
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
        var roleId = Guid.NewGuid();
        dbContext.TenantRoles.Add(new TenantRole
        {
            Id = roleId,
            TenantId = context.TenantId,
            Name = "Admin",
            NormalizedName = "ADMIN",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = context.UserId,
            TenantRoleId = roleId,
            IsActive = true,
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

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingAuditRecorder : IAuditRecorder
    {
        public Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AuditRecordResponse(
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
}
