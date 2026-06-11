using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class DataQualityTests
{
    [Fact]
    public async Task ImportValidationIssuesCreateDurableIssuesWithSourceLinksAndIdempotency()
    {
        await using var dbContext = CreateDbContext();
        var context = CreateTenantContext();
        var batch = SeedValidatedImport(dbContext, context.TenantId, context.UserId);
        var service = CreateService(dbContext, context);

        var firstRun = await service.GenerateFromImportValidationAsync(batch.Id, CancellationToken.None);
        var secondRun = await service.GenerateFromImportValidationAsync(batch.Id, CancellationToken.None);

        Assert.Equal(2, firstRun.CreatedCount);
        Assert.Equal(0, firstRun.ExistingCount);
        Assert.Equal(0, secondRun.CreatedCount);
        Assert.Equal(2, secondRun.ExistingCount);
        Assert.Equal(2, await dbContext.DataQualityIssues.CountAsync());
        Assert.All(firstRun.Issues, issue =>
        {
            Assert.Equal(DataQualityIssueOrigin.ImportValidation, issue.Origin);
            Assert.True(issue.ReviewTaskReady);
            Assert.Contains(issue.SourceLinks, link => link.SourceType == DataQualitySourceLinkType.ImportValidationIssue);
            Assert.Contains(issue.SourceLinks, link => link.SourceType == DataQualitySourceLinkType.ImportBatch);
        });
    }

    [Fact]
    public async Task ManualIssueCreationValidatesTenantAndSourceReferences()
    {
        await using var dbContext = CreateDbContext();
        var context = CreateTenantContext();
        var otherTenantId = Guid.NewGuid();
        var batch = SeedValidatedImport(dbContext, otherTenantId, context.UserId);
        var denialRecorder = new RecordingDenialRecorder();
        var service = CreateService(dbContext, context, denialRecorder: denialRecorder);

        var exception = await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            service.CreateIssueAsync(
                new CreateDataQualityIssueRequest(
                    "Manual issue",
                    "manual_issue",
                    DataQualitySeverity.Medium,
                    DataQualityAffectedEntityType.ImportBatch,
                    batch.Id,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Manual issue against wrong tenant.",
                    "Should be denied."),
                CancellationToken.None));

        Assert.Contains("not available", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(denialRecorder.Records);
        Assert.Equal("import_tenant_mismatch", denialRecorder.Records.Single().Reason);
    }

    [Fact]
    public async Task SecurityEventsCreateReviewReadyIssuesAndPreserveAuditLinks()
    {
        await using var dbContext = CreateDbContext();
        var context = CreateTenantContext();
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = context.UserId,
            EventType = SecurityEventType.SensitiveAccessAttempt,
            Severity = SecurityEventSeverity.High,
            SourceAction = "policy.denied",
            Reason = "permission_denied",
            SafeSummary = "Restricted context access was denied.",
            ReviewTaskReady = true,
            ReviewTaskHint = "Review restricted access attempt.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.SecurityEvents.Add(securityEvent);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, context);

        var issue = await service.CreateFromSecurityEventAsync(securityEvent.Id, CancellationToken.None);
        var repeated = await service.CreateFromSecurityEventAsync(securityEvent.Id, CancellationToken.None);

        Assert.Equal(issue.Id, repeated.Id);
        Assert.Equal(DataQualityIssueOrigin.SecurityEvent, issue.Origin);
        Assert.Equal(securityEvent.Id, issue.SecurityEventId);
        Assert.Equal(DataQualitySeverity.High, issue.Severity);
        Assert.Contains(issue.SourceLinks, link => link.SourceType == DataQualitySourceLinkType.SecurityEvent);
        Assert.Equal(1, await dbContext.DataQualityIssues.CountAsync());
        var updatedEvent = await dbContext.SecurityEvents.SingleAsync(item => item.Id == securityEvent.Id);
        Assert.NotNull(updatedEvent.ReviewTaskCreatedAt);
    }

    [Fact]
    public async Task SeverityTrustImpactUpdatesIdentityCandidateRecommendationExclusionAndTrustScore()
    {
        await using var dbContext = CreateDbContext();
        var context = CreateTenantContext();
        var batch = SeedValidatedImport(dbContext, context.TenantId, context.UserId);
        var candidate = new IdentityCandidateLink
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            ImportMappingVersionId = batch.MappingVersions.Single().Id,
            SourceGraphNodeId = Guid.NewGuid(),
            TargetGraphNodeId = Guid.NewGuid(),
            SourceSystem = "demo-pdm",
            TargetSystem = "demo-erp",
            SourceRecordId = "P-100",
            TargetRecordId = "P-100",
            ObjectType = "part",
            NormalizedObjectType = "PART",
            IdentityKey = "partNumber:P-100",
            ConfidenceScore = 0.9m,
            State = IdentityCandidateState.Provisional,
            TrustState = TrustState.Provisional,
            ExcludedFromTrustedRecommendations = false,
            EvidenceSummary = "Candidate evidence.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.IdentityCandidateLinks.Add(candidate);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, context);

        var issue = await service.CreateIssueAsync(
            new CreateDataQualityIssueRequest(
                "Conflicting identity evidence",
                "identity_conflict",
                DataQualitySeverity.High,
                DataQualityAffectedEntityType.IdentityCandidate,
                null,
                null,
                null,
                candidate.Id,
                null,
                null,
                null,
                "Identity candidate has conflicting evidence.",
                "Reviewer saw conflicting source systems."),
            CancellationToken.None);

        Assert.Equal(DataQualityReviewPriority.High, issue.ReviewPriority);
        Assert.True(issue.ExcludedFromTrustedRecommendations);
        Assert.Equal(0.15m, issue.TrustImpactPenalty);
        Assert.Contains(issue.TrustImpacts, impact => impact.IdentityCandidateLinkId == candidate.Id);
        var updatedCandidate = await dbContext.IdentityCandidateLinks.SingleAsync(item => item.Id == candidate.Id);
        Assert.True(updatedCandidate.ExcludedFromTrustedRecommendations);
        var trustScore = await dbContext.TrustScoreRecords.SingleAsync();
        Assert.Equal(TrustScoreEntityType.IdentityCandidate, trustScore.EntityType);
        Assert.Equal(0.75m, trustScore.Score);
    }

    [Fact]
    public async Task MonitoringPlaceholdersAreInertAndDoNotAllowLiveSourceScanning()
    {
        await using var dbContext = CreateDbContext();
        var context = CreateTenantContext();
        var service = CreateService(dbContext, context);

        var placeholders = await service.ListMonitoringPlaceholdersAsync(CancellationToken.None);

        Assert.NotEmpty(placeholders);
        Assert.All(placeholders, placeholder =>
        {
            Assert.False(placeholder.IsEnabled);
            Assert.False(placeholder.AllowsLiveSourceScanning);
        });
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        return new EnterpriseThreadDbContext(
            new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }

    private static ActiveTenantContext CreateTenantContext()
    {
        return new ActiveTenantContext(
            Guid.NewGuid(),
            "test-tenant",
            "Test Tenant",
            Guid.NewGuid());
    }

    private static ImportBatch SeedValidatedImport(EnterpriseThreadDbContext dbContext, Guid tenantId, Guid userId)
    {
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceSystem = "demo-pdm",
            NormalizedSourceSystem = "DEMO-PDM",
            Status = ImportBatchStatus.Validated,
            ActiveModelPackageVersionId = Guid.NewGuid(),
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            ValidatedAt = DateTimeOffset.UtcNow
        };
        var evidence = new ImportFileEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ImportBatchId = batch.Id,
            StorageKey = "tenant/import/demo.csv",
            Sha256Checksum = "abc123",
            OriginalFileName = "demo.csv",
            ContentType = "text/csv",
            SizeBytes = 100,
            UploadedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var mapping = new ImportMappingVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ImportBatchId = batch.Id,
            ModelPackageVersionId = batch.ActiveModelPackageVersionId,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            State = ImportMappingState.Approved,
            SuggestionProvider = "deterministic",
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            ApprovedByUserId = userId,
            ApprovedAt = DateTimeOffset.UtcNow
        };
        batch.FileEvidence.Add(evidence);
        batch.MappingVersions.Add(mapping);
        batch.ValidationIssues.Add(new ImportValidationIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ImportBatchId = batch.Id,
            ImportMappingVersionId = mapping.Id,
            Severity = ImportIssueSeverity.Error,
            RowNumber = 2,
            SourceColumn = "partNumber",
            CanonicalObjectType = "part",
            IssueCode = "missing_required_value",
            Message = "Required part number is missing.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        batch.ValidationIssues.Add(new ImportValidationIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ImportBatchId = batch.Id,
            ImportMappingVersionId = mapping.Id,
            Severity = ImportIssueSeverity.Warning,
            RowNumber = 3,
            SourceColumn = "cost",
            CanonicalObjectType = "part",
            IssueCode = "suspicious_value",
            Message = "Cost value is unusual.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ImportBatches.Add(batch);
        dbContext.ImportFileEvidence.Add(evidence);
        dbContext.ImportMappingVersions.Add(mapping);
        dbContext.ImportValidationIssues.AddRange(batch.ValidationIssues);
        dbContext.SaveChanges();
        return batch;
    }

    private static DataQualityIssueService CreateService(
        EnterpriseThreadDbContext dbContext,
        ActiveTenantContext context,
        RecordingDenialRecorder? denialRecorder = null)
    {
        return new DataQualityIssueService(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService(),
            denialRecorder ?? new RecordingDenialRecorder(),
            new RecordingAuditRecorder(dbContext));
    }

    private sealed class StaticTenantContextResolver(ActiveTenantContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
        {
            return Task.FromResult(context);
        }
    }

    private sealed class AllowAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingDenialRecorder : IAccessDenialRecorder
    {
        public List<(Guid? TenantId, Guid? UserId, string Action, string Reason, string SafeSummary)> Records { get; } = [];

        public Task RecordAsync(Guid? tenantId, Guid? userId, string action, string reason, string safeSummary, CancellationToken cancellationToken)
        {
            Records.Add((tenantId, userId, action, reason, safeSummary));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditRecorder(EnterpriseThreadDbContext dbContext) : IAuditRecorder
    {
        public async Task<AuditRecordResponse> RecordAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
        {
            var record = new AuditRecord
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                UserId = request.UserId,
                Action = request.Action,
                Result = request.Result,
                Reason = request.Reason,
                SourceObjectType = request.SourceObjectType,
                SourceObjectId = request.SourceObjectId,
                SafeSummary = request.SafeSummary,
                RetentionCategory = request.RetentionCategory,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.AuditRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new AuditRecordResponse(
                record.Id,
                record.TenantId,
                record.UserId,
                record.Action,
                record.Result,
                record.Reason,
                record.SourceObjectType,
                record.SourceObjectId,
                record.PolicyName,
                record.PolicyVersion,
                record.CorrelationId,
                record.SafeSummary,
                record.RetentionCategory,
                record.RetainUntil,
                record.IsArchiveEligible,
                record.ArchivedAt,
                record.CreatedAt);
        }

        public Task<SecurityEventResponse> RecordSecurityEventAsync(SecurityEventWriteRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Data quality tests do not record security events through the audit recorder.");
        }
    }
}
