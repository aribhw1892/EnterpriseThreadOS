using System.Text.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.IdentityResolution;

public interface IIdentityResolutionService
{
    Task<IReadOnlyCollection<IdentityResolutionRuleResponse>> ListRulesAsync(CancellationToken cancellationToken);
    Task<IdentityResolutionRuleResponse> CreateRuleAsync(CreateIdentityResolutionRuleRequest request, CancellationToken cancellationToken);
    Task<IdentityCandidateGenerationResponse> GenerateCandidatesAsync(Guid batchId, GenerateIdentityCandidatesRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<IdentityCandidateLinkResponse>> ListCandidatesAsync(Guid batchId, CancellationToken cancellationToken);
    Task<IdentityCandidateLinkResponse> ApproveCandidateAsync(Guid candidateId, IdentityReviewDecisionRequest request, CancellationToken cancellationToken);
    Task<IdentityCandidateLinkResponse> RejectCandidateAsync(Guid candidateId, IdentityReviewDecisionRequest request, CancellationToken cancellationToken);
    Task<IdentityCandidateLinkResponse> MarkCandidateConflictedAsync(Guid candidateId, IdentityReviewDecisionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TrustScoreRecordResponse>> ListTrustScoresAsync(Guid batchId, CancellationToken cancellationToken);
}

public sealed class IdentityResolutionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IImportFileStorage fileStorage,
    IImportFileParser fileParser,
    IGraphMemoryService graphMemoryService) : IIdentityResolutionService
{
    private const string IdentityRelationshipType = "IDENTITY_LINK";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CreateIdentityResolutionRuleRequestValidator RuleValidator = new();

    public async Task<IReadOnlyCollection<IdentityResolutionRuleResponse>> ListRulesAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.rules.list", IdentityResolutionPermissions.Read, cancellationToken);
        var rules = await dbContext.IdentityResolutionRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == context.TenantId)
            .OrderBy(rule => rule.ObjectType)
            .ThenBy(rule => rule.Name)
            .ToListAsync(cancellationToken);
        return rules.Select(ToRuleResponse).ToList();
    }

    public async Task<IdentityResolutionRuleResponse> CreateRuleAsync(CreateIdentityResolutionRuleRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(RuleValidator, request, cancellationToken);
        var context = await RequirePermissionAsync("identity_resolution.rules.create", IdentityResolutionPermissions.Manage, cancellationToken);
        var normalizedName = NormalizeKey(request.Name);
        var exists = await dbContext.IdentityResolutionRules.AnyAsync(
            rule => rule.TenantId == context.TenantId && rule.NormalizedName == normalizedName,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Identity resolution rule name already exists for this tenant.");
        }

        var rule = new IdentityResolutionRule
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Name = NormalizeText(request.Name),
            NormalizedName = normalizedName,
            ObjectType = NormalizeText(request.ObjectType),
            NormalizedObjectType = NormalizeKey(request.ObjectType),
            IdentityAttributeKeysJson = JsonSerializer.Serialize(request.IdentityAttributeKeys.Select(NormalizeText).ToArray(), JsonOptions),
            AutoApproveThreshold = request.AutoApproveThreshold,
            ReviewThreshold = request.ReviewThreshold,
            IsActive = true,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.IdentityResolutionRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "identity_resolution.rules.create", $"Identity resolution rule '{rule.Name}' was created.", nameof(IdentityResolutionRule), rule.Id, cancellationToken);
        return ToRuleResponse(rule);
    }

    public async Task<IdentityCandidateGenerationResponse> GenerateCandidatesAsync(
        Guid batchId,
        GenerateIdentityCandidatesRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.candidates.generate", IdentityResolutionPermissions.Manage, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "identity_resolution.candidates.generate", cancellationToken);
        if (batch.Status != ImportBatchStatus.Staged)
        {
            throw new RequestValidationException("Identity candidates can only be generated for staged import batches.");
        }

        var mapping = GetApprovedMapping(batch);
        var stagingRun = GetCompletedStagingRun(batch);
        var rule = await ResolveRuleAsync(context, request.RuleId, mapping, cancellationToken);
        var ruleKeys = DeserializeStringArray(rule.IdentityAttributeKeysJson)
            .Select(NormalizeKey)
            .ToHashSet(StringComparer.Ordinal);
        var currentRecords = await LoadIndexedRecordsAsync(batch, mapping, stagingRun, ruleKeys, cancellationToken);
        var comparisonBatches = await dbContext.ImportBatches
            .Include(item => item.FileEvidence)
            .Include(item => item.MappingVersions)
            .ThenInclude(item => item.ColumnMappings)
            .Include(item => item.MappingVersions)
            .ThenInclude(item => item.LifecycleMappings)
            .Include(item => item.ValidationIssues)
            .Include(item => item.StagingRuns)
            .Where(item => item.TenantId == context.TenantId && item.Id != batch.Id && item.Status == ImportBatchStatus.Staged)
            .ToListAsync(cancellationToken);

        var comparisonRecords = new List<IdentityIndexedRecord>();
        foreach (var comparisonBatch in comparisonBatches)
        {
            var comparisonMapping = GetApprovedMapping(comparisonBatch);
            var comparisonRun = GetCompletedStagingRun(comparisonBatch);
            comparisonRecords.AddRange(await LoadIndexedRecordsAsync(comparisonBatch, comparisonMapping, comparisonRun, ruleKeys, cancellationToken));
        }

        var existingCandidates = await dbContext.IdentityCandidateLinks
            .Include(candidate => candidate.Decisions)
            .Where(candidate => candidate.TenantId == context.TenantId && candidate.ImportBatchId == batch.Id)
            .ToListAsync(cancellationToken);
        var existingKeys = existingCandidates
            .Select(candidate => CandidateKey(candidate.SourceGraphNodeId, candidate.TargetGraphNodeId, candidate.IdentityKey))
            .ToHashSet(StringComparer.Ordinal);
        var created = new List<IdentityCandidateLink>();

        foreach (var source in currentRecords)
        {
            foreach (var target in comparisonRecords)
            {
                if (!string.Equals(source.NormalizedObjectType, target.NormalizedObjectType, StringComparison.Ordinal)
                    || string.Equals(source.NormalizedSourceSystem, target.NormalizedSourceSystem, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(source.IdentityKey)
                    || !string.Equals(source.IdentityKey, target.IdentityKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var score = CalculateConfidence(source, target, ruleKeys);
                if (score < rule.ReviewThreshold)
                {
                    continue;
                }

                var candidateKey = CandidateKey(source.GraphNodeId, target.GraphNodeId, source.IdentityKey);
                if (existingKeys.Contains(candidateKey))
                {
                    continue;
                }

                var candidate = new IdentityCandidateLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    ImportBatchId = batch.Id,
                    ImportMappingVersionId = mapping.Id,
                    ImportStagingGraphRunId = stagingRun.Id,
                    IdentityResolutionRuleId = rule.Id,
                    SourceGraphNodeId = source.GraphNodeId,
                    TargetGraphNodeId = target.GraphNodeId,
                    SourceSystem = source.SourceSystem,
                    TargetSystem = target.SourceSystem,
                    SourceRecordId = source.SourceRecordId,
                    TargetRecordId = target.SourceRecordId,
                    ObjectType = source.ObjectType,
                    NormalizedObjectType = source.NormalizedObjectType,
                    IdentityKey = source.IdentityKey,
                    ConfidenceScore = score,
                    State = score >= rule.AutoApproveThreshold ? IdentityCandidateState.Provisional : IdentityCandidateState.Unverified,
                    TrustState = score >= rule.AutoApproveThreshold ? TrustState.Provisional : TrustState.Unverified,
                    ExcludedFromTrustedRecommendations = true,
                    EvidenceSummary = BuildEvidenceSummary(source, target, score),
                    CreatedAt = DateTimeOffset.UtcNow
                };
                created.Add(candidate);
                existingKeys.Add(candidateKey);
            }
        }

        MarkGeneratedConflicts(created, existingCandidates);
        dbContext.IdentityCandidateLinks.AddRange(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var candidate in created)
        {
            await RecalculateCandidateTrustScoreAsync(candidate, batch.ValidationIssues, cancellationToken);
        }

        if (created.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RecordAuditAsync(context, "identity_resolution.candidates.generate", $"Generated {created.Count} identity candidate link(s) for import batch.", nameof(ImportBatch), batch.Id, cancellationToken);
        var candidates = existingCandidates.Concat(created)
            .OrderByDescending(candidate => candidate.ConfidenceScore)
            .ThenBy(candidate => candidate.CreatedAt)
            .Select(ToCandidateResponse)
            .ToList();
        return new IdentityCandidateGenerationResponse(batch.Id, created.Count, existingCandidates.Count, candidates);
    }

    public async Task<IReadOnlyCollection<IdentityCandidateLinkResponse>> ListCandidatesAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.candidates.list", IdentityResolutionPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "identity_resolution.candidates.list", cancellationToken);
        var candidates = await dbContext.IdentityCandidateLinks
            .AsNoTracking()
            .Include(candidate => candidate.Decisions)
            .Where(candidate => candidate.TenantId == context.TenantId && candidate.ImportBatchId == batch.Id)
            .OrderByDescending(candidate => candidate.ConfidenceScore)
            .ThenBy(candidate => candidate.CreatedAt)
            .ToListAsync(cancellationToken);
        return candidates.Select(ToCandidateResponse).ToList();
    }

    public async Task<IdentityCandidateLinkResponse> ApproveCandidateAsync(
        Guid candidateId,
        IdentityReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.candidates.approve", IdentityResolutionPermissions.Review, cancellationToken);
        var candidate = await RequireCandidateAsync(candidateId, context, "identity_resolution.candidates.approve", cancellationToken);
        if (candidate.State == IdentityCandidateState.Approved)
        {
            return ToCandidateResponse(candidate);
        }

        if (candidate.State is IdentityCandidateState.Rejected or IdentityCandidateState.Conflicted)
        {
            throw new RequestValidationException("Rejected or conflicted identity candidates cannot be approved.");
        }

        var resultingTrust = candidate.ConfidenceScore >= 0.97m ? TrustState.Trusted : TrustState.Provisional;
        var relationship = candidate.GraphRelationshipId is null
            ? await graphMemoryService.CreateRelationshipAsync(
                new CreateGraphRelationshipRequest(
                    context.TenantId,
                    candidate.SourceGraphNodeId,
                    candidate.TargetGraphNodeId,
                    IdentityRelationshipType,
                    resultingTrust,
                    new Dictionary<string, string?>
                    {
                        ["identityKey"] = candidate.IdentityKey,
                        ["confidenceScore"] = candidate.ConfidenceScore.ToString("0.###"),
                        ["reviewState"] = IdentityDecisionType.Approved.ToString()
                    },
                    new GraphSourceReference("identity-resolution", candidate.Id.ToString(), candidate.ImportBatchId.ToString())),
                cancellationToken)
            : null;

        candidate.State = IdentityCandidateState.Approved;
        candidate.TrustState = resultingTrust;
        candidate.ExcludedFromTrustedRecommendations = resultingTrust != TrustState.Trusted;
        candidate.GraphRelationshipId ??= relationship?.RelationshipId;
        candidate.ReviewedByUserId = context.UserId;
        candidate.ReviewedAt = DateTimeOffset.UtcNow;
        AddDecisionAndLearning(context, candidate, IdentityDecisionType.Approved, resultingTrust, request.Rationale);
        await RecalculateCandidateTrustScoreAsync(candidate, candidate.ImportBatch?.ValidationIssues ?? [], cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "identity_resolution.candidates.approve", "Identity candidate was approved and represented as a graph relationship.", nameof(IdentityCandidateLink), candidate.Id, cancellationToken);
        return ToCandidateResponse(candidate);
    }

    public async Task<IdentityCandidateLinkResponse> RejectCandidateAsync(
        Guid candidateId,
        IdentityReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.candidates.reject", IdentityResolutionPermissions.Review, cancellationToken);
        var candidate = await RequireCandidateAsync(candidateId, context, "identity_resolution.candidates.reject", cancellationToken);
        if (candidate.State == IdentityCandidateState.Approved)
        {
            throw new RequestValidationException("Approved identity candidates cannot be rejected.");
        }

        candidate.State = IdentityCandidateState.Rejected;
        candidate.TrustState = TrustState.Unverified;
        candidate.ExcludedFromTrustedRecommendations = true;
        candidate.ReviewedByUserId = context.UserId;
        candidate.ReviewedAt = DateTimeOffset.UtcNow;
        AddDecisionAndLearning(context, candidate, IdentityDecisionType.Rejected, TrustState.Unverified, request.Rationale);
        await RecalculateCandidateTrustScoreAsync(candidate, candidate.ImportBatch?.ValidationIssues ?? [], cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "identity_resolution.candidates.reject", "Identity candidate was rejected and captured as learning evidence.", nameof(IdentityCandidateLink), candidate.Id, cancellationToken);
        return ToCandidateResponse(candidate);
    }

    public async Task<IdentityCandidateLinkResponse> MarkCandidateConflictedAsync(
        Guid candidateId,
        IdentityReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.candidates.conflict", IdentityResolutionPermissions.Review, cancellationToken);
        var candidate = await RequireCandidateAsync(candidateId, context, "identity_resolution.candidates.conflict", cancellationToken);
        if (candidate.State == IdentityCandidateState.Approved)
        {
            throw new RequestValidationException("Approved identity candidates cannot be marked conflicted.");
        }

        candidate.State = IdentityCandidateState.Conflicted;
        candidate.TrustState = TrustState.Conflicted;
        candidate.ExcludedFromTrustedRecommendations = true;
        candidate.ReviewedByUserId = context.UserId;
        candidate.ReviewedAt = DateTimeOffset.UtcNow;
        AddDecisionAndLearning(context, candidate, IdentityDecisionType.Conflicted, TrustState.Conflicted, request.Rationale);
        await RecalculateCandidateTrustScoreAsync(candidate, candidate.ImportBatch?.ValidationIssues ?? [], cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "identity_resolution.candidates.conflict", "Identity candidate was marked conflicted and excluded from trusted recommendation use.", nameof(IdentityCandidateLink), candidate.Id, cancellationToken);
        return ToCandidateResponse(candidate);
    }

    public async Task<IReadOnlyCollection<TrustScoreRecordResponse>> ListTrustScoresAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("identity_resolution.trust_scores.list", IdentityResolutionPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "identity_resolution.trust_scores.list", cancellationToken);
        var scores = await dbContext.TrustScoreRecords
            .AsNoTracking()
            .Where(score => score.TenantId == context.TenantId && score.ImportBatchId == batch.Id)
            .OrderByDescending(score => score.RecalculatedAt)
            .ToListAsync(cancellationToken);
        return scores.Select(ToTrustScoreResponse).ToList();
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityResolutionPermissions.Admin, cancellationToken)
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
            throw new TenantAccessDeniedException("User lacks identity resolution permission.");
        }

        return context;
    }

    private async Task<ImportBatch> RequireBatchAsync(Guid batchId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .Include(item => item.FileEvidence)
            .Include(item => item.MappingVersions)
            .ThenInclude(item => item.ColumnMappings)
            .Include(item => item.MappingVersions)
            .ThenInclude(item => item.LifecycleMappings)
            .Include(item => item.ValidationIssues)
            .Include(item => item.StagingRuns)
            .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken)
            ?? throw new RequestValidationException("Import batch was not found.");
        await EnsureSameTenantAsync(batch.TenantId, context, action, "import_tenant_mismatch", "The requested import batch belongs to a different tenant.", cancellationToken);
        return batch;
    }

    private async Task<IdentityCandidateLink> RequireCandidateAsync(Guid candidateId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var candidate = await dbContext.IdentityCandidateLinks
            .Include(item => item.ImportBatch)
            .ThenInclude(item => item!.ValidationIssues)
            .Include(item => item.Decisions)
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken)
            ?? throw new RequestValidationException("Identity candidate was not found.");
        await EnsureSameTenantAsync(candidate.TenantId, context, action, "identity_candidate_tenant_mismatch", "The requested identity candidate belongs to a different tenant.", cancellationToken);
        return candidate;
    }

    private async Task EnsureSameTenantAsync(Guid resourceTenantId, ActiveTenantContext context, string action, string reason, string safeSummary, CancellationToken cancellationToken)
    {
        if (resourceTenantId == context.TenantId)
        {
            return;
        }

        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, reason, safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("Identity resolution resource is not available in the active tenant.");
    }

    private async Task<IdentityResolutionRule> ResolveRuleAsync(
        ActiveTenantContext context,
        Guid? ruleId,
        ImportMappingVersion mapping,
        CancellationToken cancellationToken)
    {
        if (ruleId is not null)
        {
            var explicitRule = await dbContext.IdentityResolutionRules.SingleOrDefaultAsync(rule => rule.Id == ruleId.Value, cancellationToken)
                ?? throw new RequestValidationException("Identity resolution rule was not found.");
            await EnsureSameTenantAsync(explicitRule.TenantId, context, "identity_resolution.rules.resolve", "identity_rule_tenant_mismatch", "The requested identity rule belongs to a different tenant.", cancellationToken);
            return explicitRule;
        }

        var identityMappings = mapping.ColumnMappings.Where(column => column.IsIdentityField).ToList();
        if (identityMappings.Count == 0)
        {
            throw new RequestValidationException("Identity candidate generation requires at least one identity field mapping.");
        }

        var objectType = identityMappings.First().CanonicalObjectType;
        var normalizedObjectType = NormalizeKey(objectType);
        var identityKeys = identityMappings
            .Select(mapping => mapping.CanonicalAttributeKey ?? mapping.SourceColumn)
            .Select(NormalizeText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serializedKeys = JsonSerializer.Serialize(identityKeys, JsonOptions);
        var existing = await dbContext.IdentityResolutionRules
            .FirstOrDefaultAsync(
                rule => rule.TenantId == context.TenantId
                    && rule.NormalizedObjectType == normalizedObjectType
                    && rule.IdentityAttributeKeysJson == serializedKeys
                    && rule.IsActive,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var rule = new IdentityResolutionRule
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Name = $"Default {objectType} identity",
            NormalizedName = NormalizeKey($"Default {objectType} identity {Guid.NewGuid():N}"),
            ObjectType = objectType,
            NormalizedObjectType = normalizedObjectType,
            IdentityAttributeKeysJson = serializedKeys,
            AutoApproveThreshold = 0.97m,
            ReviewThreshold = 0.6m,
            IsActive = true,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.IdentityResolutionRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rule;
    }

    private async Task<IReadOnlyCollection<IdentityIndexedRecord>> LoadIndexedRecordsAsync(
        ImportBatch batch,
        ImportMappingVersion mapping,
        ImportStagingGraphRun stagingRun,
        IReadOnlySet<string> ruleIdentityKeys,
        CancellationToken cancellationToken)
    {
        var evidence = batch.FileEvidence.OrderByDescending(item => item.CreatedAt).FirstOrDefault()
            ?? throw new RequestValidationException("Import batch does not have file evidence.");
        await using var stream = await fileStorage.OpenReadAsync(evidence.StorageKey, cancellationToken);
        var parsed = await fileParser.ParseAsync(evidence.OriginalFileName, stream, null, cancellationToken);
        var nodeIds = DeserializeGuidArray(stagingRun.GraphNodeIdsJson).ToList();
        var identityMappings = mapping.ColumnMappings
            .Where(item => item.IsIdentityField)
            .Where(item => ruleIdentityKeys.Count == 0
                || ruleIdentityKeys.Contains(NormalizeKey(item.CanonicalAttributeKey ?? item.SourceColumn)))
            .ToList();
        if (identityMappings.Count == 0)
        {
            identityMappings = mapping.ColumnMappings.Where(item => item.IsIdentityField).ToList();
        }

        var records = new List<IdentityIndexedRecord>();
        var rowIndex = 0;
        foreach (var row in parsed.Rows)
        {
            if (rowIndex >= nodeIds.Count)
            {
                break;
            }

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var identityMapping in identityMappings)
            {
                var key = identityMapping.CanonicalAttributeKey ?? identityMapping.SourceColumn;
                values[key] = row.TryGetValue(identityMapping.SourceColumn, out var value) ? NormalizeOptional(value) : null;
            }

            var sourceRecordId = string.Join("|", identityMappings
                .Select(identityMapping => row.TryGetValue(identityMapping.SourceColumn, out var value) ? value : null)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
            records.Add(new IdentityIndexedRecord(
                batch.Id,
                mapping.Id,
                stagingRun.Id,
                nodeIds[rowIndex],
                identityMappings.First().CanonicalObjectType,
                NormalizeKey(identityMappings.First().CanonicalObjectType),
                batch.SourceSystem,
                batch.NormalizedSourceSystem,
                sourceRecordId,
                string.Join("|", values.OrderBy(item => item.Key).Select(item => NormalizeKey(item.Value ?? string.Empty))),
                values,
                ResolveLifecycleValue(row, mapping),
                batch.ValidationIssues.Count(issue => issue.Severity == ImportIssueSeverity.Error)));
            rowIndex++;
        }

        return records;
    }

    private static ImportMappingVersion GetApprovedMapping(ImportBatch batch)
    {
        return batch.MappingVersions
            .Where(mapping => mapping.State == ImportMappingState.Approved)
            .OrderByDescending(mapping => mapping.ApprovedAt)
            .FirstOrDefault()
            ?? throw new RequestValidationException("An approved import mapping is required before identity resolution.");
    }

    private static ImportStagingGraphRun GetCompletedStagingRun(ImportBatch batch)
    {
        return batch.StagingRuns
            .Where(run => run.Status == ImportStagingRunStatus.Completed)
            .OrderByDescending(run => run.CompletedAt)
            .FirstOrDefault()
            ?? throw new RequestValidationException("A completed staging graph run is required before identity resolution.");
    }

    private static decimal CalculateConfidence(IdentityIndexedRecord source, IdentityIndexedRecord target, IReadOnlySet<string> ruleKeys)
    {
        var consideredKeys = source.IdentityValues.Keys
            .Where(key => ruleKeys.Count == 0 || ruleKeys.Contains(NormalizeKey(key)))
            .ToList();
        if (consideredKeys.Count == 0)
        {
            consideredKeys = source.IdentityValues.Keys.ToList();
        }

        var matchingKeys = consideredKeys.Count(key =>
            source.IdentityValues.TryGetValue(key, out var sourceValue)
            && target.IdentityValues.TryGetValue(key, out var targetValue)
            && !string.IsNullOrWhiteSpace(sourceValue)
            && string.Equals(NormalizeKey(sourceValue), NormalizeKey(targetValue ?? string.Empty), StringComparison.Ordinal));
        var identityComponent = consideredKeys.Count == 0 ? 0m : (decimal)matchingKeys / consideredKeys.Count * 0.75m;
        var lifecycleComponent = !string.IsNullOrWhiteSpace(source.LifecycleState)
            && string.Equals(source.LifecycleState, target.LifecycleState, StringComparison.OrdinalIgnoreCase)
                ? 0.1m
                : 0m;
        var sourceSystemComponent = !string.Equals(source.NormalizedSourceSystem, target.NormalizedSourceSystem, StringComparison.Ordinal)
            ? 0.1m
            : 0m;
        var validationComponent = source.ValidationErrorCount == 0 && target.ValidationErrorCount == 0 ? 0.05m : 0m;
        return Math.Round(Math.Min(1m, identityComponent + lifecycleComponent + sourceSystemComponent + validationComponent), 3);
    }

    private static void MarkGeneratedConflicts(
        IReadOnlyCollection<IdentityCandidateLink> created,
        IReadOnlyCollection<IdentityCandidateLink> existingCandidates)
    {
        var candidates = created.Concat(existingCandidates).ToList();
        var conflictedSourceKeys = candidates
            .Where(candidate => candidate.State != IdentityCandidateState.Rejected)
            .GroupBy(candidate => $"{candidate.SourceGraphNodeId:N}:{candidate.IdentityKey}", StringComparer.Ordinal)
            .Where(group => group.Select(candidate => candidate.TargetGraphNodeId).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var conflictedTargetKeys = candidates
            .Where(candidate => candidate.State != IdentityCandidateState.Rejected)
            .GroupBy(candidate => $"{candidate.TargetGraphNodeId:N}:{candidate.IdentityKey}", StringComparer.Ordinal)
            .Where(group => group.Select(candidate => candidate.SourceGraphNodeId).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in created)
        {
            if (conflictedSourceKeys.Contains($"{candidate.SourceGraphNodeId:N}:{candidate.IdentityKey}")
                || conflictedTargetKeys.Contains($"{candidate.TargetGraphNodeId:N}:{candidate.IdentityKey}"))
            {
                candidate.State = IdentityCandidateState.Conflicted;
                candidate.TrustState = TrustState.Conflicted;
                candidate.ExcludedFromTrustedRecommendations = true;
            }
        }
    }

    private void AddDecisionAndLearning(
        ActiveTenantContext context,
        IdentityCandidateLink candidate,
        IdentityDecisionType decisionType,
        TrustState trustState,
        string? rationale)
    {
        var decision = new IdentityResolutionDecision
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            IdentityCandidateLinkId = candidate.Id,
            DecisionType = decisionType,
            ResultingTrustState = trustState,
            Rationale = TrimOptional(rationale),
            DecidedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var evidence = new IdentityLearningEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            IdentityCandidateLinkId = candidate.Id,
            IdentityResolutionDecisionId = decision.Id,
            Outcome = decisionType,
            IdentityKey = candidate.IdentityKey,
            EvidenceSummary = $"Reviewer marked identity candidate {candidate.Id} as {decisionType}.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        candidate.Decisions.Add(decision);
        dbContext.IdentityResolutionDecisions.Add(decision);
        dbContext.IdentityLearningEvidence.Add(evidence);
    }

    private async Task RecalculateCandidateTrustScoreAsync(
        IdentityCandidateLink candidate,
        IReadOnlyCollection<ImportValidationIssue> validationIssues,
        CancellationToken cancellationToken)
    {
        var validationPenalty = validationIssues.Count(issue => issue.Severity == ImportIssueSeverity.Error) * 0.05m
            + validationIssues.Count(issue => issue.Severity == ImportIssueSeverity.Warning) * 0.02m;
        var decisionComponent = candidate.State switch
        {
            IdentityCandidateState.Approved => 0.2m,
            IdentityCandidateState.Provisional => 0.1m,
            IdentityCandidateState.Unverified => 0m,
            IdentityCandidateState.Rejected => -0.35m,
            IdentityCandidateState.Conflicted => -0.5m,
            _ => 0m
        };
        var conflictPenalty = candidate.TrustState == TrustState.Conflicted ? 0.35m : 0m;
        var score = Math.Round(Math.Clamp(candidate.ConfidenceScore + decisionComponent - validationPenalty - conflictPenalty, 0m, 1m), 3);
        var trustState = candidate.State switch
        {
            IdentityCandidateState.Approved when score >= 0.97m => TrustState.Trusted,
            IdentityCandidateState.Approved => TrustState.Provisional,
            IdentityCandidateState.Conflicted => TrustState.Conflicted,
            IdentityCandidateState.Rejected => TrustState.Unverified,
            IdentityCandidateState.Provisional => TrustState.Provisional,
            IdentityCandidateState.Unverified => TrustState.Unverified,
            _ => TrustState.Unverified
        };
        var breakdown = new Dictionary<string, decimal>
        {
            ["candidateConfidence"] = candidate.ConfidenceScore,
            ["decisionComponent"] = decisionComponent,
            ["validationPenalty"] = validationPenalty,
            ["conflictPenalty"] = conflictPenalty
        };
        var existing = await dbContext.TrustScoreRecords.SingleOrDefaultAsync(
            record => record.TenantId == candidate.TenantId
                && record.ImportBatchId == candidate.ImportBatchId
                && record.IdentityCandidateLinkId == candidate.Id
                && record.EntityType == TrustScoreEntityType.IdentityCandidate,
            cancellationToken);
        if (existing is null)
        {
            dbContext.TrustScoreRecords.Add(new TrustScoreRecord
            {
                Id = Guid.NewGuid(),
                TenantId = candidate.TenantId,
                ImportBatchId = candidate.ImportBatchId,
                IdentityCandidateLinkId = candidate.Id,
                GraphRelationshipId = candidate.GraphRelationshipId,
                EntityType = TrustScoreEntityType.IdentityCandidate,
                Score = score,
                TrustState = trustState,
                BreakdownJson = JsonSerializer.Serialize(breakdown, JsonOptions),
                RecalculatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        existing.GraphRelationshipId = candidate.GraphRelationshipId;
        existing.Score = score;
        existing.TrustState = trustState;
        existing.BreakdownJson = JsonSerializer.Serialize(breakdown, JsonOptions);
        existing.RecalculatedAt = DateTimeOffset.UtcNow;
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

    private static string BuildEvidenceSummary(IdentityIndexedRecord source, IdentityIndexedRecord target, decimal score)
    {
        return $"Matched {source.ObjectType} identity '{source.SourceRecordId}' from {source.SourceSystem} to '{target.SourceRecordId}' from {target.SourceSystem} with confidence {score:0.###}.";
    }

    private static string? ResolveLifecycleValue(IReadOnlyDictionary<string, string?> row, ImportMappingVersion mapping)
    {
        foreach (var lifecycleMapping in mapping.LifecycleMappings)
        {
            if (row.Values.Any(value => !string.IsNullOrWhiteSpace(value) && NormalizeKey(value!) == lifecycleMapping.NormalizedSourceValue))
            {
                return lifecycleMapping.CanonicalLifecycleKey;
            }
        }

        return null;
    }

    private static IdentityResolutionRuleResponse ToRuleResponse(IdentityResolutionRule rule)
    {
        return new IdentityResolutionRuleResponse(
            rule.Id,
            rule.TenantId,
            rule.Name,
            rule.ObjectType,
            DeserializeStringArray(rule.IdentityAttributeKeysJson),
            rule.AutoApproveThreshold,
            rule.ReviewThreshold,
            rule.IsActive,
            rule.CreatedByUserId,
            rule.CreatedAt);
    }

    private static IdentityCandidateLinkResponse ToCandidateResponse(IdentityCandidateLink candidate)
    {
        return new IdentityCandidateLinkResponse(
            candidate.Id,
            candidate.TenantId,
            candidate.ImportBatchId,
            candidate.ImportMappingVersionId,
            candidate.ImportStagingGraphRunId,
            candidate.IdentityResolutionRuleId,
            candidate.SourceGraphNodeId,
            candidate.TargetGraphNodeId,
            candidate.SourceSystem,
            candidate.TargetSystem,
            candidate.SourceRecordId,
            candidate.TargetRecordId,
            candidate.ObjectType,
            candidate.IdentityKey,
            candidate.ConfidenceScore,
            candidate.State,
            candidate.TrustState,
            candidate.ExcludedFromTrustedRecommendations,
            candidate.GraphRelationshipId,
            candidate.EvidenceSummary,
            candidate.CreatedAt,
            candidate.ReviewedByUserId,
            candidate.ReviewedAt,
            candidate.Decisions.OrderByDescending(decision => decision.CreatedAt).Select(ToDecisionResponse).ToList());
    }

    private static IdentityResolutionDecisionResponse ToDecisionResponse(IdentityResolutionDecision decision)
    {
        return new IdentityResolutionDecisionResponse(
            decision.Id,
            decision.TenantId,
            decision.IdentityCandidateLinkId,
            decision.DecisionType,
            decision.ResultingTrustState,
            decision.Rationale,
            decision.DecidedByUserId,
            decision.CreatedAt);
    }

    private static TrustScoreRecordResponse ToTrustScoreResponse(TrustScoreRecord score)
    {
        return new TrustScoreRecordResponse(
            score.Id,
            score.TenantId,
            score.ImportBatchId,
            score.IdentityCandidateLinkId,
            score.GraphNodeId,
            score.GraphRelationshipId,
            score.EntityType,
            score.Score,
            score.TrustState,
            string.IsNullOrWhiteSpace(score.BreakdownJson)
                ? new Dictionary<string, decimal>()
                : JsonSerializer.Deserialize<IReadOnlyDictionary<string, decimal>>(score.BreakdownJson, JsonOptions) ?? new Dictionary<string, decimal>(),
            score.RecalculatedAt);
    }

    private static IReadOnlyCollection<Guid> DeserializeGuidArray(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyCollection<Guid>>(json, JsonOptions) ?? [];
    }

    private static IReadOnlyCollection<string> DeserializeStringArray(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyCollection<string>>(json, JsonOptions) ?? [];
    }

    private static string CandidateKey(Guid sourceGraphNodeId, Guid targetGraphNodeId, string identityKey)
    {
        return $"{sourceGraphNodeId:N}:{targetGraphNodeId:N}:{identityKey}";
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
        }
    }

    private static string NormalizeText(string value) => value.Trim();
    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record IdentityIndexedRecord(
        Guid BatchId,
        Guid MappingVersionId,
        Guid StagingRunId,
        Guid GraphNodeId,
        string ObjectType,
        string NormalizedObjectType,
        string SourceSystem,
        string NormalizedSourceSystem,
        string SourceRecordId,
        string IdentityKey,
        IReadOnlyDictionary<string, string?> IdentityValues,
        string? LifecycleState,
        int ValidationErrorCount);

    private sealed class CreateIdentityResolutionRuleRequestValidator : AbstractValidator<CreateIdentityResolutionRuleRequest>
    {
        public CreateIdentityResolutionRuleRequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
            RuleFor(request => request.ObjectType).NotEmpty().MaximumLength(120);
            RuleFor(request => request.IdentityAttributeKeys).NotEmpty();
            RuleForEach(request => request.IdentityAttributeKeys).NotEmpty().MaximumLength(160);
            RuleFor(request => request.AutoApproveThreshold).InclusiveBetween(0.01m, 1m);
            RuleFor(request => request.ReviewThreshold).InclusiveBetween(0.01m, 1m);
            RuleFor(request => request).Must(request => request.AutoApproveThreshold >= request.ReviewThreshold)
                .WithMessage("Auto approve threshold must be greater than or equal to review threshold.");
        }
    }
}
