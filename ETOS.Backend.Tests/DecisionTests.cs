using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Decisions;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using ETOS.Backend.Outcomes;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class DecisionConflictResolverTests
{
    [Fact]
    public void AllRequiredConflictWhenApproveAndReject()
    {
        var resolver = new DecisionConflictResolver();
        var userOne = Guid.NewGuid();
        var userTwo = Guid.NewGuid();
        var payload = DecisionPayloadParser.CreateFromReviewTask(
            new ReviewTaskPayloadParser.ReviewTaskPayloadDocument
            {
                Title = "Conflict test",
                SourceType = ReviewTaskSourceType.Manual,
                SourceReference = "manual",
                ReviewTaskType = "business-action-review",
                Participants =
                [
                    new ReviewTaskPayloadParser.ReviewTaskParticipantDocument { UserId = userOne, Role = ReviewTaskParticipantRole.Approver },
                    new ReviewTaskPayloadParser.ReviewTaskParticipantDocument { UserId = userTwo, Role = ReviewTaskParticipantRole.Approver }
                ]
            },
            "accept",
            "summary",
            "reason",
            new DecisionPayloadParser.DecisionApprovalRuleSnapshotDocument
            {
                Mode = DecisionApprovalRuleMode.AllRequired
            },
            DecisionStatus.PendingVotes,
            DecisionConflictState.None);

        var evaluation = resolver.Evaluate(
            payload,
            [
                new DecisionVote { UserId = userOne, Vote = DecisionVoteKind.Approve },
                new DecisionVote { UserId = userTwo, Vote = DecisionVoteKind.Reject }
            ],
            new Dictionary<Guid, ReviewTaskParticipantRole>
            {
                [userOne] = ReviewTaskParticipantRole.Approver,
                [userTwo] = ReviewTaskParticipantRole.Approver
            });

        Assert.Equal(DecisionStatus.BlockedConflict, evaluation.Status);
        Assert.Equal(DecisionConflictState.Blocked, evaluation.ConflictState);
    }

    [Fact]
    public void MajorityResolvesWhenMoreApprovals()
    {
        var resolver = new DecisionConflictResolver();
        var users = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var payload = DecisionPayloadParser.CreateFromReviewTask(
            new ReviewTaskPayloadParser.ReviewTaskPayloadDocument
            {
                Title = "Majority test",
                SourceType = ReviewTaskSourceType.Manual,
                SourceReference = "manual",
                ReviewTaskType = "business-action-review",
                Participants = users.Select(id => new ReviewTaskPayloadParser.ReviewTaskParticipantDocument
                {
                    UserId = id,
                    Role = ReviewTaskParticipantRole.Approver
                }).ToList()
            },
            "accept",
            "summary",
            "reason",
            new DecisionPayloadParser.DecisionApprovalRuleSnapshotDocument
            {
                Mode = DecisionApprovalRuleMode.Majority
            },
            DecisionStatus.PendingVotes,
            DecisionConflictState.None);

        var evaluation = resolver.Evaluate(
            payload,
            [
                new DecisionVote { UserId = users[0], Vote = DecisionVoteKind.Approve },
                new DecisionVote { UserId = users[1], Vote = DecisionVoteKind.Approve },
                new DecisionVote { UserId = users[2], Vote = DecisionVoteKind.Reject }
            ],
            users.ToDictionary(id => id, _ => ReviewTaskParticipantRole.Approver));

        Assert.Equal(DecisionStatus.Finalized, evaluation.Status);
        Assert.Equal("accept", evaluation.OutcomeKey);
    }
}

public sealed class DecisionTests
{
    [Fact]
    public async Task CompletedReviewTaskCreatesDecisionArtifact()
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
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Accepted, "Accepted.", null),
            CancellationToken.None);

        Assert.False(completed.DecisionCreationDeferred);
        Assert.NotNull(completed.DecisionArtifactId);

        var decision = await dbContext.Artifacts.SingleAsync(artifact => artifact.Id == completed.DecisionArtifactId);
        Assert.Equal(DecisionArtifactTypes.Decision, decision.ArtifactType);
    }

    [Fact]
    public async Task RejectedCompletionCreatesDecisionWithRejectOutcome()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "access-request-review", requiresDq: false);

        var created = await CreateReviewTaskFactory(dbContext, context).CreateManualAsync(
            new CreateReviewTaskRequest(
                "Reject path",
                "access-request-review",
                ReviewTaskSourceType.Manual,
                "manual-reject",
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

        var completed = await CreateReviewTaskService(dbContext, context).CompleteAsync(
            created.ArtifactId,
            created.VersionId,
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Rejected, "Rejected.", null),
            CancellationToken.None);

        var version = await dbContext.ArtifactVersions
            .SingleAsync(item => item.ArtifactId == completed.DecisionArtifactId);
        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        Assert.Equal("reject", payload.OutcomeKey);
        Assert.Equal(DecisionStatus.Finalized, payload.Status);
    }

    [Fact]
    public async Task NoActionOutcomeKeyFinalizesDecision()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "data-quality-review", requiresDq: false);

        var created = await CreateReviewTaskFactory(dbContext, context).CreateManualAsync(
            new CreateReviewTaskRequest(
                "No action",
                "data-quality-review",
                ReviewTaskSourceType.Manual,
                "manual-no-action",
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

        var completed = await CreateReviewTaskService(dbContext, context).CompleteAsync(
            created.ArtifactId,
            created.VersionId,
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Accepted, "No action needed.", "no_action"),
            CancellationToken.None);

        var version = await dbContext.ArtifactVersions
            .SingleAsync(item => item.ArtifactId == completed.DecisionArtifactId);
        var payload = DecisionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        Assert.Equal("no_action", payload.OutcomeKey);
        Assert.Equal(DecisionStatus.Finalized, payload.Status);
    }

    [Fact]
    public async Task LearningEvidenceCreatedOnFinalize()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "data-quality-review", requiresDq: false);

        var created = await CreateReviewTaskFactory(dbContext, context).CreateManualAsync(
            new CreateReviewTaskRequest(
                "Learning evidence",
                "data-quality-review",
                ReviewTaskSourceType.Manual,
                "manual-learning",
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

        var completed = await CreateReviewTaskService(dbContext, context).CompleteAsync(
            created.ArtifactId,
            created.VersionId,
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Accepted, "Done.", null),
            CancellationToken.None);

        var evidenceCount = await dbContext.DecisionLearningEvidence
            .CountAsync(item => item.TenantId == context.TenantId && item.DecisionArtifactId == completed.DecisionArtifactId);
        Assert.True(evidenceCount >= 1);
    }

    [Fact]
    public async Task ManualOutcomeLinkedToDecision()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        await SeedPublishedTemplateAsync(dbContext, context, "data-quality-review", requiresDq: false);

        var created = await CreateReviewTaskFactory(dbContext, context).CreateManualAsync(
            new CreateReviewTaskRequest(
                "Outcome link",
                "data-quality-review",
                ReviewTaskSourceType.Manual,
                "manual-outcome",
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

        var completed = await CreateReviewTaskService(dbContext, context).CompleteAsync(
            created.ArtifactId,
            created.VersionId,
            new CompleteReviewTaskRequest(ReviewTaskCompletionResolution.Accepted, "Done.", null),
            CancellationToken.None);

        var outcomeService = CreateOutcomeService(dbContext, context);
        var recorded = await outcomeService.RecordManualOutcomeAsync(
            completed.DecisionArtifactId!.Value,
            completed.DecisionVersionId!.Value,
            new RecordManualOutcomeRequest(
                "manual-check",
                "expected-alignment",
                "approved",
                OutcomeCheckStatus.Successful,
                0.95m,
                "Manual verification complete.",
                null),
            CancellationToken.None);

        Assert.Equal(completed.DecisionArtifactId, recorded.DecisionArtifactId);
        var run = await dbContext.OutcomeCheckRuns.SingleAsync(item => item.Id == recorded.OutcomeCheckRunId);
        Assert.Equal("approved", run.ActualOutcome);
    }

    [Fact]
    public async Task LearningSignalRollupAtThreshold()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenantWithMembership(dbContext);
        var rollup = new LearningSignalRollupService(dbContext, Options.Create(new LearningSignalRollupOptions
        {
            MinOccurrences = 3,
            WindowDays = 30
        }));

        var payload = DecisionPayloadParser.CreateFromReviewTask(
            new ReviewTaskPayloadParser.ReviewTaskPayloadDocument
            {
                Title = "Rollup",
                SourceType = ReviewTaskSourceType.Manual,
                SourceReference = "manual",
                ReviewTaskType = "data-quality-review"
            },
            "accept",
            "summary",
            "reason",
            DecisionPayloadParser.DefaultApprovalRule(),
            DecisionStatus.Finalized,
            DecisionConflictState.None);

        for (var i = 0; i < 3; i++)
        {
            dbContext.DecisionLearningEvidence.Add(new DecisionLearningEvidence
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                PatternKey = LearningEvidenceEmitter.BuildPatternKey(payload),
                SourceType = "manual",
                OutcomeKey = "accept",
                EvidenceSummary = $"Evidence {i}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
        await rollup.EvaluateAsync(context.TenantId, context.UserId, payload, CancellationToken.None);

        var signalExists = await dbContext.Artifacts.AnyAsync(
            artifact => artifact.TenantId == context.TenantId
                && artifact.NormalizedArtifactType == LearningArtifactTypes.LearningSignal.ToUpperInvariant());
        Assert.True(signalExists);
    }

    private static OutcomeService CreateOutcomeService(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            new RecordingDenialRecorder(),
            new LearningEvidenceEmitter(dbContext));

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
            CreateDecisionCompletionHandler(dbContext));

    private static DecisionReviewTaskCompletionHandler CreateDecisionCompletionHandler(EnterpriseThreadDbContext dbContext)
    {
        var rollupOptions = Options.Create(new LearningSignalRollupOptions());
        return new DecisionReviewTaskCompletionHandler(new DecisionFactory(
            dbContext,
            new DecisionConflictResolver(),
            new LearningEvidenceEmitter(dbContext),
            new LearningSignalRollupService(dbContext, rollupOptions)));
    }

    private static async Task SeedPublishedTemplateAsync(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        string templateKey,
        bool requiresDq)
    {
        var payload = ReviewTaskTemplatePayloadParser.Create(
            templateKey,
            templateKey,
            null,
            requiresDq,
            null,
            null,
            null,
            ["accept", "reject", "no_action", "defer"]);
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

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenantWithMembership(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
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

    private sealed record TestContext(Guid TenantId, Guid UserId, Guid GraphNodeId);

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
