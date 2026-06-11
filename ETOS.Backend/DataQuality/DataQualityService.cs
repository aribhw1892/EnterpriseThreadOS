using System.Text.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.DataQuality;

public interface IDataQualityIssueService
{
    Task<IReadOnlyCollection<DataQualityIssueResponse>> ListIssuesAsync(CancellationToken cancellationToken);
    Task<DataQualityIssueResponse> GetIssueAsync(Guid issueId, CancellationToken cancellationToken);
    Task<DataQualityIssueResponse> CreateIssueAsync(CreateDataQualityIssueRequest request, CancellationToken cancellationToken);
    Task<GenerateDataQualityIssuesFromImportResponse> GenerateFromImportValidationAsync(Guid batchId, CancellationToken cancellationToken);
    Task<DataQualityIssueResponse> CreateFromSecurityEventAsync(Guid securityEventId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MonitoringIssueTypeDefinitionResponse>> ListMonitoringPlaceholdersAsync(CancellationToken cancellationToken);
}

public sealed class DataQualityIssueService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder) : IDataQualityIssueService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CreateDataQualityIssueRequestValidator CreateValidator = new();

    public async Task<IReadOnlyCollection<DataQualityIssueResponse>> ListIssuesAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("data_quality.issues.list", DataQualityPermissions.Read, cancellationToken);
        var issues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Include(issue => issue.SourceLinks)
            .Include(issue => issue.TrustImpacts)
            .Where(issue => issue.TenantId == context.TenantId)
            .OrderByDescending(issue => issue.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return issues.Select(ToIssueResponse).ToList();
    }

    public async Task<DataQualityIssueResponse> GetIssueAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("data_quality.issues.get", DataQualityPermissions.Read, cancellationToken);
        var issue = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Include(item => item.SourceLinks)
            .Include(item => item.TrustImpacts)
            .SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken)
            ?? throw new RequestValidationException("Data quality issue was not found.");
        await EnsureSameTenantAsync(issue.TenantId, context, "data_quality.issues.get", "data_quality_tenant_mismatch", "The requested data quality issue belongs to a different tenant.", cancellationToken);
        return ToIssueResponse(issue);
    }

    public async Task<DataQualityIssueResponse> CreateIssueAsync(CreateDataQualityIssueRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateValidator, request, cancellationToken);
        var context = await RequirePermissionAsync("data_quality.issues.create", DataQualityPermissions.Manage, cancellationToken);
        var source = await ResolveManualSourceAsync(context, request, cancellationToken);
        var severityProfile = BuildSeverityProfile(request.Severity, source.ConflictSensitive);
        var issue = new DataQualityIssue
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Title = TrimToMax(request.Title, 200),
            IssueCode = TrimToMax(request.IssueCode, 120),
            NormalizedIssueCode = NormalizeKey(request.IssueCode),
            Severity = request.Severity,
            Status = DataQualityIssueStatus.Open,
            Origin = DataQualityIssueOrigin.Manual,
            AffectedEntityType = request.AffectedEntityType,
            ImportBatchId = source.ImportBatchId,
            ImportValidationIssueId = source.ImportValidationIssueId,
            ImportFileEvidenceId = source.ImportFileEvidenceId,
            IdentityCandidateLinkId = source.IdentityCandidateLinkId,
            GraphNodeId = request.GraphNodeId,
            GraphRelationshipId = request.GraphRelationshipId,
            TrustImpactPenalty = severityProfile.Penalty,
            ResultingTrustState = severityProfile.ResultingTrustState,
            ExcludedFromTrustedRecommendations = severityProfile.ExcludedFromTrustedRecommendations,
            ReviewPriority = severityProfile.ReviewPriority,
            ReviewTaskReady = true,
            ReviewTaskHint = BuildReviewHint(request.Severity, request.AffectedEntityType),
            ReviewHookCreatedAt = DateTimeOffset.UtcNow,
            EvidenceSummary = TrimToMax(request.EvidenceSummary, 1000),
            Rationale = TrimOptional(request.Rationale),
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        AddSourceLink(issue, source.SourceType, source.SourceId, source.Label, source.SafeSummary);
        AddTrustImpact(issue, request.AffectedEntityType, source.IdentityCandidateLinkId, request.GraphNodeId, request.GraphRelationshipId, severityProfile);

        dbContext.DataQualityIssues.Add(issue);
        await ApplyTrustImpactAsync(issue, severityProfile, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "data_quality.issues.create", $"Data quality issue '{issue.IssueCode}' was created.", nameof(DataQualityIssue), issue.Id, cancellationToken);
        return ToIssueResponse(issue);
    }

    public async Task<GenerateDataQualityIssuesFromImportResponse> GenerateFromImportValidationAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("data_quality.import_issues.generate", DataQualityPermissions.Manage, cancellationToken);
        var batch = await dbContext.ImportBatches
            .Include(item => item.ValidationIssues)
            .Include(item => item.FileEvidence)
            .Include(item => item.StagingRuns)
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new RequestValidationException("Import batch was not found.");
        await EnsureSameTenantAsync(batch.TenantId, context, "data_quality.import_issues.generate", "import_tenant_mismatch", "The requested import batch belongs to a different tenant.", cancellationToken);
        if (batch.Status is not (ImportBatchStatus.Validated or ImportBatchStatus.Staged))
        {
            throw new RequestValidationException("Data quality issues can only be generated for validated or staged import batches.");
        }

        var existingKeys = await dbContext.DataQualityIssues
            .Where(issue => issue.TenantId == context.TenantId && issue.UniqueSourceKey != null)
            .Select(issue => issue.UniqueSourceKey!)
            .ToListAsync(cancellationToken);
        var existingKeySet = existingKeys.ToHashSet(StringComparer.Ordinal);
        var created = new List<DataQualityIssue>();
        var existingCount = 0;
        var completedRun = batch.StagingRuns
            .Where(run => run.Status == ImportStagingRunStatus.Completed)
            .OrderByDescending(run => run.CompletedAt ?? run.CreatedAt)
            .FirstOrDefault();
        var primaryEvidence = batch.FileEvidence.OrderByDescending(evidence => evidence.CreatedAt).FirstOrDefault();

        foreach (var validationIssue in batch.ValidationIssues.OrderBy(issue => issue.CreatedAt))
        {
            var uniqueSourceKey = BuildImportValidationKey(context.TenantId, validationIssue.Id);
            if (existingKeySet.Contains(uniqueSourceKey))
            {
                existingCount++;
                continue;
            }

            var severity = validationIssue.Severity == ImportIssueSeverity.Error
                ? DataQualitySeverity.High
                : DataQualitySeverity.Medium;
            var severityProfile = BuildSeverityProfile(severity, conflictSensitive: false);
            var issue = new DataQualityIssue
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                Title = TrimToMax($"{validationIssue.IssueCode}: {validationIssue.Message}", 200),
                IssueCode = validationIssue.IssueCode,
                NormalizedIssueCode = NormalizeKey(validationIssue.IssueCode),
                Severity = severity,
                Status = DataQualityIssueStatus.Open,
                Origin = DataQualityIssueOrigin.ImportValidation,
                AffectedEntityType = DataQualityAffectedEntityType.ImportValidationIssue,
                ImportBatchId = batch.Id,
                ImportMappingVersionId = validationIssue.ImportMappingVersionId,
                ImportStagingGraphRunId = completedRun?.Id,
                ImportValidationIssueId = validationIssue.Id,
                ImportFileEvidenceId = primaryEvidence?.Id,
                TrustImpactPenalty = severityProfile.Penalty,
                ResultingTrustState = severityProfile.ResultingTrustState,
                ExcludedFromTrustedRecommendations = severityProfile.ExcludedFromTrustedRecommendations,
                ReviewPriority = severityProfile.ReviewPriority,
                ReviewTaskReady = true,
                ReviewTaskHint = BuildReviewHint(severity, DataQualityAffectedEntityType.ImportValidationIssue),
                ReviewHookCreatedAt = DateTimeOffset.UtcNow,
                UniqueSourceKey = uniqueSourceKey,
                EvidenceSummary = BuildImportValidationEvidenceSummary(validationIssue),
                CreatedByUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            AddSourceLink(issue, DataQualitySourceLinkType.ImportBatch, batch.Id.ToString(), "Import batch", $"Import batch '{batch.SourceSystem}' validation issue source.");
            AddSourceLink(issue, DataQualitySourceLinkType.ImportValidationIssue, validationIssue.Id.ToString(), validationIssue.IssueCode, validationIssue.Message);
            if (validationIssue.ImportMappingVersionId is not null)
            {
                AddSourceLink(issue, DataQualitySourceLinkType.ImportMappingVersion, validationIssue.ImportMappingVersionId.Value.ToString(), "Import mapping version", "Approved import mapping associated with the validation issue.");
            }

            if (primaryEvidence is not null)
            {
                AddSourceLink(issue, DataQualitySourceLinkType.ImportFileEvidence, primaryEvidence.Id.ToString(), primaryEvidence.OriginalFileName, "Raw import file evidence associated with the validation issue.");
            }

            if (completedRun is not null)
            {
                AddSourceLink(issue, DataQualitySourceLinkType.ImportStagingGraphRun, completedRun.Id.ToString(), "Staging graph run", "Completed staging graph run associated with the validation issue.");
            }

            AddTrustImpact(issue, DataQualityAffectedEntityType.ImportValidationIssue, null, null, null, severityProfile);
            dbContext.DataQualityIssues.Add(issue);
            created.Add(issue);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "data_quality.import_issues.generate", $"Generated {created.Count} data quality issue(s) from import validation.", nameof(ImportBatch), batch.Id, cancellationToken);
        var issueIds = created.Select(issue => issue.Id).ToList();
        var responseIssues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Include(issue => issue.SourceLinks)
            .Include(issue => issue.TrustImpacts)
            .Where(issue => issue.TenantId == context.TenantId && (issue.ImportBatchId == batch.Id || issueIds.Contains(issue.Id)))
            .OrderByDescending(issue => issue.CreatedAt)
            .ToListAsync(cancellationToken);
        return new GenerateDataQualityIssuesFromImportResponse(batch.Id, created.Count, existingCount, responseIssues.Select(ToIssueResponse).ToList());
    }

    public async Task<DataQualityIssueResponse> CreateFromSecurityEventAsync(Guid securityEventId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("data_quality.security_events.create_issue", DataQualityPermissions.ReviewHook, cancellationToken);
        var securityEvent = await dbContext.SecurityEvents.SingleOrDefaultAsync(item => item.Id == securityEventId, cancellationToken)
            ?? throw new RequestValidationException("Security event was not found.");
        if (securityEvent.TenantId is null)
        {
            throw new RequestValidationException("Security event is not tenant-scoped and cannot create a data quality review hook.");
        }

        await EnsureSameTenantAsync(securityEvent.TenantId.Value, context, "data_quality.security_events.create_issue", "security_event_tenant_mismatch", "The requested security event belongs to a different tenant.", cancellationToken);
        if (!securityEvent.ReviewTaskReady)
        {
            throw new RequestValidationException("Security event is not marked review-task-ready.");
        }

        var uniqueSourceKey = BuildSecurityEventKey(context.TenantId, securityEvent.Id);
        var existing = await dbContext.DataQualityIssues
            .Include(issue => issue.SourceLinks)
            .Include(issue => issue.TrustImpacts)
            .SingleOrDefaultAsync(issue => issue.TenantId == context.TenantId && issue.UniqueSourceKey == uniqueSourceKey, cancellationToken);
        if (existing is not null)
        {
            return ToIssueResponse(existing);
        }

        var severity = MapSecuritySeverity(securityEvent.Severity);
        var severityProfile = BuildSeverityProfile(severity, conflictSensitive: true);
        var issue = new DataQualityIssue
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Title = TrimToMax($"Security review hook: {securityEvent.EventType}", 200),
            IssueCode = $"security_{NormalizeKey(securityEvent.EventType.ToString()).ToLowerInvariant()}",
            NormalizedIssueCode = NormalizeKey($"security_{securityEvent.EventType}"),
            Severity = severity,
            Status = DataQualityIssueStatus.Open,
            Origin = DataQualityIssueOrigin.SecurityEvent,
            AffectedEntityType = DataQualityAffectedEntityType.SecurityEvent,
            SecurityEventId = securityEvent.Id,
            TrustImpactPenalty = severityProfile.Penalty,
            ResultingTrustState = severityProfile.ResultingTrustState,
            ExcludedFromTrustedRecommendations = severityProfile.ExcludedFromTrustedRecommendations,
            ReviewPriority = severityProfile.ReviewPriority,
            ReviewTaskReady = true,
            ReviewTaskHint = securityEvent.ReviewTaskHint ?? BuildReviewHint(severity, DataQualityAffectedEntityType.SecurityEvent),
            ReviewHookCreatedAt = DateTimeOffset.UtcNow,
            UniqueSourceKey = uniqueSourceKey,
            EvidenceSummary = securityEvent.SafeSummary,
            Rationale = securityEvent.Reason,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        AddSourceLink(issue, DataQualitySourceLinkType.SecurityEvent, securityEvent.Id.ToString(), securityEvent.EventType.ToString(), securityEvent.SafeSummary);
        AddTrustImpact(issue, DataQualityAffectedEntityType.SecurityEvent, null, null, null, severityProfile);
        dbContext.DataQualityIssues.Add(issue);
        securityEvent.ReviewTaskCreatedAt = DateTimeOffset.UtcNow;
        securityEvent.ReviewTaskHint = $"Data quality issue {issue.Id} created as the review hook output.";
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "data_quality.security_events.create_issue", $"Data quality issue created from security event '{securityEvent.EventType}'.", nameof(SecurityEvent), securityEvent.Id, cancellationToken);
        return ToIssueResponse(issue);
    }

    public async Task<IReadOnlyCollection<MonitoringIssueTypeDefinitionResponse>> ListMonitoringPlaceholdersAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("data_quality.monitoring_placeholders.list", DataQualityPermissions.Read, cancellationToken);
        await EnsureMonitoringPlaceholdersAsync(context, cancellationToken);
        return await dbContext.MonitoringIssueTypeDefinitions
            .AsNoTracking()
            .Where(definition => definition.TenantId == context.TenantId)
            .OrderBy(definition => definition.IssueTypeKey)
            .Select(definition => new MonitoringIssueTypeDefinitionResponse(
                definition.Id,
                definition.TenantId,
                definition.IssueTypeKey,
                definition.DisplayName,
                definition.SafeSummary,
                definition.IsEnabled,
                definition.AllowsLiveSourceScanning,
                definition.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureMonitoringPlaceholdersAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        var placeholders = new[]
        {
            ("post_import_issue_monitor", "Post-import issue monitor", "Disabled placeholder for monitoring already-created import and data-quality issue types."),
            ("identity_conflict_monitor", "Identity conflict monitor", "Disabled placeholder for inspecting existing identity conflict issue types after import review."),
            ("security_review_hook_monitor", "Security review hook monitor", "Disabled placeholder for inspecting existing security-event review hook issue types.")
        };
        foreach (var placeholder in placeholders)
        {
            var normalized = NormalizeKey(placeholder.Item1);
            var exists = await dbContext.MonitoringIssueTypeDefinitions.AnyAsync(
                definition => definition.TenantId == context.TenantId && definition.NormalizedIssueTypeKey == normalized,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.MonitoringIssueTypeDefinitions.Add(new MonitoringIssueTypeDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                IssueTypeKey = placeholder.Item1,
                NormalizedIssueTypeKey = normalized,
                DisplayName = placeholder.Item2,
                SafeSummary = placeholder.Item3,
                IsEnabled = false,
                AllowsLiveSourceScanning = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ManualSourceContext> ResolveManualSourceAsync(
        ActiveTenantContext context,
        CreateDataQualityIssueRequest request,
        CancellationToken cancellationToken)
    {
        return request.AffectedEntityType switch
        {
            DataQualityAffectedEntityType.ImportBatch => await ResolveImportBatchSourceAsync(context, request, cancellationToken),
            DataQualityAffectedEntityType.ImportValidationIssue => await ResolveImportValidationSourceAsync(context, request, cancellationToken),
            DataQualityAffectedEntityType.IdentityCandidate => await ResolveIdentityCandidateSourceAsync(context, request, cancellationToken),
            DataQualityAffectedEntityType.GraphNode => ResolveGraphSource(request.GraphNodeId, DataQualitySourceLinkType.GraphNode, "Graph node"),
            DataQualityAffectedEntityType.GraphRelationship => ResolveGraphSource(request.GraphRelationshipId, DataQualitySourceLinkType.GraphRelationship, "Graph relationship"),
            DataQualityAffectedEntityType.GenericSource => ResolveGenericSource(request),
            DataQualityAffectedEntityType.SecurityEvent => throw new RequestValidationException("Use the security event review-hook endpoint for security event issues."),
            _ => throw new RequestValidationException("Unsupported data quality issue source.")
        };
    }

    private async Task<ManualSourceContext> ResolveImportBatchSourceAsync(
        ActiveTenantContext context,
        CreateDataQualityIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ImportBatchId is null)
        {
            throw new RequestValidationException("Import batch source requires ImportBatchId.");
        }

        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ImportBatchId.Value, cancellationToken)
            ?? throw new RequestValidationException("Import batch was not found.");
        await EnsureSameTenantAsync(batch.TenantId, context, "data_quality.issues.create", "import_tenant_mismatch", "The requested import batch belongs to a different tenant.", cancellationToken);
        return new ManualSourceContext(
            DataQualitySourceLinkType.ImportBatch,
            batch.Id.ToString(),
            "Import batch",
            $"Import batch '{batch.SourceSystem}' was referenced by a manual data quality issue.",
            batch.Id,
            null,
            null,
            null,
            ConflictSensitive: false);
    }

    private async Task<ManualSourceContext> ResolveImportValidationSourceAsync(
        ActiveTenantContext context,
        CreateDataQualityIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ImportValidationIssueId is null)
        {
            throw new RequestValidationException("Import validation issue source requires ImportValidationIssueId.");
        }

        var validationIssue = await dbContext.ImportValidationIssues
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ImportValidationIssueId.Value, cancellationToken)
            ?? throw new RequestValidationException("Import validation issue was not found.");
        await EnsureSameTenantAsync(validationIssue.TenantId, context, "data_quality.issues.create", "import_validation_tenant_mismatch", "The requested import validation issue belongs to a different tenant.", cancellationToken);
        return new ManualSourceContext(
            DataQualitySourceLinkType.ImportValidationIssue,
            validationIssue.Id.ToString(),
            validationIssue.IssueCode,
            validationIssue.Message,
            validationIssue.ImportBatchId,
            validationIssue.Id,
            null,
            null,
            ConflictSensitive: false);
    }

    private async Task<ManualSourceContext> ResolveIdentityCandidateSourceAsync(
        ActiveTenantContext context,
        CreateDataQualityIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IdentityCandidateLinkId is null)
        {
            throw new RequestValidationException("Identity candidate source requires IdentityCandidateLinkId.");
        }

        var candidate = await dbContext.IdentityCandidateLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.IdentityCandidateLinkId.Value, cancellationToken)
            ?? throw new RequestValidationException("Identity candidate was not found.");
        await EnsureSameTenantAsync(candidate.TenantId, context, "data_quality.issues.create", "identity_candidate_tenant_mismatch", "The requested identity candidate belongs to a different tenant.", cancellationToken);
        return new ManualSourceContext(
            DataQualitySourceLinkType.IdentityCandidate,
            candidate.Id.ToString(),
            "Identity candidate",
            candidate.EvidenceSummary,
            candidate.ImportBatchId,
            null,
            null,
            candidate.Id,
            ConflictSensitive: candidate.TrustState == TrustState.Conflicted || candidate.State == IdentityCandidateState.Conflicted);
    }

    private static ManualSourceContext ResolveGraphSource(Guid? graphId, DataQualitySourceLinkType linkType, string label)
    {
        if (graphId is null)
        {
            throw new RequestValidationException($"{label} source requires a graph identifier.");
        }

        return new ManualSourceContext(linkType, graphId.Value.ToString(), label, $"{label} referenced by a manual data quality issue.", null, null, null, null, ConflictSensitive: true);
    }

    private static ManualSourceContext ResolveGenericSource(CreateDataQualityIssueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GenericSourceId))
        {
            throw new RequestValidationException("Generic source issues require GenericSourceId.");
        }

        return new ManualSourceContext(DataQualitySourceLinkType.GenericSource, TrimToMax(request.GenericSourceId, 200), "Generic source", "Generic platform context referenced by a manual data quality issue.", request.ImportBatchId, null, null, null, ConflictSensitive: false);
    }

    private async Task ApplyTrustImpactAsync(
        DataQualityIssue issue,
        SeverityProfile severityProfile,
        CancellationToken cancellationToken)
    {
        if (issue.IdentityCandidateLinkId is null || issue.ImportBatchId is null)
        {
            return;
        }

        var candidate = await dbContext.IdentityCandidateLinks.SingleOrDefaultAsync(
            item => item.TenantId == issue.TenantId && item.Id == issue.IdentityCandidateLinkId.Value,
            cancellationToken);
        if (candidate is null)
        {
            return;
        }

        if (severityProfile.ExcludedFromTrustedRecommendations)
        {
            candidate.ExcludedFromTrustedRecommendations = true;
            if (severityProfile.ResultingTrustState == TrustState.Conflicted)
            {
                candidate.TrustState = TrustState.Conflicted;
                candidate.State = IdentityCandidateState.Conflicted;
            }
        }

        var existing = await dbContext.TrustScoreRecords.SingleOrDefaultAsync(
            record => record.TenantId == issue.TenantId
                && record.ImportBatchId == issue.ImportBatchId.Value
                && record.IdentityCandidateLinkId == issue.IdentityCandidateLinkId.Value
                && record.EntityType == TrustScoreEntityType.IdentityCandidate,
            cancellationToken);
        var currentScore = existing?.Score ?? candidate.ConfidenceScore;
        var newScore = Math.Round(Math.Clamp(currentScore - severityProfile.Penalty, 0m, 1m), 3);
        var breakdown = new Dictionary<string, decimal>
        {
            ["previousScore"] = currentScore,
            ["dataQualityPenalty"] = severityProfile.Penalty
        };
        if (existing is null)
        {
            dbContext.TrustScoreRecords.Add(new TrustScoreRecord
            {
                Id = Guid.NewGuid(),
                TenantId = issue.TenantId,
                ImportBatchId = issue.ImportBatchId.Value,
                IdentityCandidateLinkId = issue.IdentityCandidateLinkId.Value,
                GraphRelationshipId = candidate.GraphRelationshipId,
                EntityType = TrustScoreEntityType.IdentityCandidate,
                Score = newScore,
                TrustState = severityProfile.ResultingTrustState,
                BreakdownJson = JsonSerializer.Serialize(breakdown, JsonOptions),
                RecalculatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        existing.Score = newScore;
        existing.TrustState = severityProfile.ResultingTrustState;
        existing.BreakdownJson = JsonSerializer.Serialize(breakdown, JsonOptions);
        existing.RecalculatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DataQualityPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks data quality permission.");
        }

        return context;
    }

    private async Task EnsureSameTenantAsync(
        Guid resourceTenantId,
        ActiveTenantContext context,
        string action,
        string reason,
        string safeSummary,
        CancellationToken cancellationToken)
    {
        if (resourceTenantId == context.TenantId)
        {
            return;
        }

        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, reason, safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("Data quality resource is not available in the active tenant.");
    }

    private async Task<AuditRecordResponse> RecordAuditAsync(
        ActiveTenantContext context,
        string action,
        string safeSummary,
        string sourceObjectType,
        Guid sourceObjectId,
        CancellationToken cancellationToken)
    {
        return await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                AuditResult.Success,
                null,
                safeSummary,
                SourceObjectType: sourceObjectType,
                SourceObjectId: sourceObjectId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);
    }

    private static void AddSourceLink(
        DataQualityIssue issue,
        DataQualitySourceLinkType sourceType,
        string sourceId,
        string? label,
        string safeSummary)
    {
        issue.SourceLinks.Add(new DataQualityIssueSourceLink
        {
            Id = Guid.NewGuid(),
            TenantId = issue.TenantId,
            DataQualityIssueId = issue.Id,
            SourceType = sourceType,
            SourceId = TrimToMax(sourceId, 200),
            Label = TrimOptional(label),
            SafeSummary = TrimToMax(safeSummary, 1000),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static void AddTrustImpact(
        DataQualityIssue issue,
        DataQualityAffectedEntityType targetEntityType,
        Guid? identityCandidateLinkId,
        Guid? graphNodeId,
        Guid? graphRelationshipId,
        SeverityProfile severityProfile)
    {
        issue.TrustImpacts.Add(new DataQualityTrustImpact
        {
            Id = Guid.NewGuid(),
            TenantId = issue.TenantId,
            DataQualityIssueId = issue.Id,
            TargetEntityType = targetEntityType,
            IdentityCandidateLinkId = identityCandidateLinkId,
            GraphNodeId = graphNodeId,
            GraphRelationshipId = graphRelationshipId,
            ScorePenalty = severityProfile.Penalty,
            ResultingTrustState = severityProfile.ResultingTrustState,
            ExcludedFromTrustedRecommendations = severityProfile.ExcludedFromTrustedRecommendations,
            BreakdownJson = JsonSerializer.Serialize(severityProfile.Breakdown, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static SeverityProfile BuildSeverityProfile(DataQualitySeverity severity, bool conflictSensitive)
    {
        var penalty = severity switch
        {
            DataQualitySeverity.Low => 0.02m,
            DataQualitySeverity.Medium => 0.05m,
            DataQualitySeverity.High => 0.15m,
            DataQualitySeverity.Critical => 0.3m,
            _ => 0.05m
        };
        var resultingTrustState = severity switch
        {
            DataQualitySeverity.Critical => TrustState.Conflicted,
            DataQualitySeverity.High when conflictSensitive => TrustState.Conflicted,
            DataQualitySeverity.High => TrustState.Provisional,
            DataQualitySeverity.Medium => TrustState.Provisional,
            DataQualitySeverity.Low => TrustState.Unverified,
            _ => TrustState.Unverified
        };
        var priority = severity switch
        {
            DataQualitySeverity.Critical => DataQualityReviewPriority.Critical,
            DataQualitySeverity.High => DataQualityReviewPriority.High,
            DataQualitySeverity.Medium => DataQualityReviewPriority.Normal,
            DataQualitySeverity.Low => DataQualityReviewPriority.Low,
            _ => DataQualityReviewPriority.Normal
        };
        var excluded = severity >= DataQualitySeverity.High || resultingTrustState == TrustState.Conflicted;
        return new SeverityProfile(
            penalty,
            resultingTrustState,
            excluded,
            priority,
            new Dictionary<string, decimal>
            {
                ["severityPenalty"] = penalty,
                ["conflictSensitive"] = conflictSensitive ? 1m : 0m
            });
    }

    private static DataQualitySeverity MapSecuritySeverity(SecurityEventSeverity severity)
    {
        return severity switch
        {
            SecurityEventSeverity.Low => DataQualitySeverity.Low,
            SecurityEventSeverity.Medium => DataQualitySeverity.Medium,
            SecurityEventSeverity.High => DataQualitySeverity.High,
            SecurityEventSeverity.Critical => DataQualitySeverity.Critical,
            _ => DataQualitySeverity.Medium
        };
    }

    private static string BuildReviewHint(DataQualitySeverity severity, DataQualityAffectedEntityType affectedEntityType)
    {
        return $"{severity} data quality issue on {affectedEntityType}; ready for later review task creation.";
    }

    private static string BuildImportValidationEvidenceSummary(ImportValidationIssue issue)
    {
        var row = issue.RowNumber is null ? "batch-level" : $"row {issue.RowNumber}";
        var column = string.IsNullOrWhiteSpace(issue.SourceColumn) ? "unknown column" : issue.SourceColumn;
        return $"{issue.IssueCode} at {row}, {column}: {issue.Message}";
    }

    internal static DataQualityIssueResponse ToIssueResponse(DataQualityIssue issue)
    {
        return new DataQualityIssueResponse(
            issue.Id,
            issue.TenantId,
            issue.Title,
            issue.IssueCode,
            issue.Severity,
            issue.Status,
            issue.Origin,
            issue.AffectedEntityType,
            issue.ImportBatchId,
            issue.ImportMappingVersionId,
            issue.ImportStagingGraphRunId,
            issue.ImportValidationIssueId,
            issue.ImportFileEvidenceId,
            issue.IdentityCandidateLinkId,
            issue.SecurityEventId,
            issue.GraphNodeId,
            issue.GraphRelationshipId,
            issue.TrustImpactPenalty,
            issue.ResultingTrustState,
            issue.ExcludedFromTrustedRecommendations,
            issue.ReviewPriority,
            issue.ReviewTaskReady,
            issue.ReviewTaskHint,
            issue.ReviewHookCreatedAt,
            issue.UniqueSourceKey,
            issue.EvidenceSummary,
            issue.Rationale,
            issue.CreatedByUserId,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.SourceLinks.OrderBy(link => link.CreatedAt).Select(ToSourceLinkResponse).ToList(),
            issue.TrustImpacts.OrderBy(impact => impact.CreatedAt).Select(ToTrustImpactResponse).ToList());
    }

    private static DataQualityIssueSourceLinkResponse ToSourceLinkResponse(DataQualityIssueSourceLink link)
    {
        return new DataQualityIssueSourceLinkResponse(
            link.Id,
            link.TenantId,
            link.DataQualityIssueId,
            link.SourceType,
            link.SourceId,
            link.Label,
            link.SafeSummary,
            link.CreatedAt);
    }

    private static DataQualityTrustImpactResponse ToTrustImpactResponse(DataQualityTrustImpact impact)
    {
        return new DataQualityTrustImpactResponse(
            impact.Id,
            impact.TenantId,
            impact.DataQualityIssueId,
            impact.TargetEntityType,
            impact.GraphNodeId,
            impact.GraphRelationshipId,
            impact.IdentityCandidateLinkId,
            impact.ScorePenalty,
            impact.ResultingTrustState,
            impact.ExcludedFromTrustedRecommendations,
            DeserializeDecimalDictionary(impact.BreakdownJson),
            impact.CreatedAt);
    }

    private static IReadOnlyDictionary<string, decimal> DeserializeDecimalDictionary(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOptions) ?? new Dictionary<string, decimal>();
    }

    private static string BuildImportValidationKey(Guid tenantId, Guid validationIssueId) => $"import-validation:{tenantId:N}:{validationIssueId:N}";

    private static string BuildSecurityEventKey(Guid tenantId, Guid securityEventId) => $"security-event:{tenantId:N}:{securityEventId:N}";

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();

    private static string TrimToMax(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TrimToMax(value, 1000);
    }

    private static async Task ValidateAsync<TRequest>(
        IValidator<TRequest> validator,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
        }
    }

    private sealed record ManualSourceContext(
        DataQualitySourceLinkType SourceType,
        string SourceId,
        string? Label,
        string SafeSummary,
        Guid? ImportBatchId,
        Guid? ImportValidationIssueId,
        Guid? ImportFileEvidenceId,
        Guid? IdentityCandidateLinkId,
        bool ConflictSensitive);

    private sealed record SeverityProfile(
        decimal Penalty,
        TrustState ResultingTrustState,
        bool ExcludedFromTrustedRecommendations,
        DataQualityReviewPriority ReviewPriority,
        IReadOnlyDictionary<string, decimal> Breakdown);
}

public sealed class CreateDataQualityIssueRequestValidator : AbstractValidator<CreateDataQualityIssueRequest>
{
    public CreateDataQualityIssueRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.IssueCode).NotEmpty().MaximumLength(120);
        RuleFor(request => request.EvidenceSummary).NotEmpty().MaximumLength(1000);
        RuleFor(request => request.Rationale).MaximumLength(1000);
    }
}
