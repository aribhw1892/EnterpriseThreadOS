using System.Globalization;
using System.Text.Json;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Imports;

public interface IImportService
{
    Task<IReadOnlyCollection<ImportBatchResponse>> ListBatchesAsync(CancellationToken cancellationToken);
    Task<ImportBatchDetailResponse> GetBatchAsync(Guid batchId, CancellationToken cancellationToken);
    Task<ImportBatchResponse> CreateBatchAsync(CreateImportBatchRequest request, CancellationToken cancellationToken);
    Task<UploadImportFileResponse> UploadFileAsync(Guid batchId, IFormFile file, CancellationToken cancellationToken);
    Task<ImportPreviewResponse> PreviewMappingAsync(Guid batchId, ImportPreviewRequest request, CancellationToken cancellationToken);
    Task<ImportMappingVersionResponse> CreateMappingVersionAsync(CreateImportMappingVersionRequest request, CancellationToken cancellationToken);
    Task<ImportMappingVersionResponse> ApproveMappingVersionAsync(Guid mappingVersionId, ApproveImportMappingRequest request, CancellationToken cancellationToken);
    Task<ImportValidationResponse> ValidateBatchAsync(Guid batchId, CancellationToken cancellationToken);
    Task<ImportStagingGraphRunResponse> StageBatchAsync(Guid batchId, CancellationToken cancellationToken);
    Task<ImportPromotionRunResponse> PromoteBatchAsync(Guid batchId, CancellationToken cancellationToken);
    Task<RejectedStagingSummaryResponse> RejectStagingAsync(Guid batchId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ImportPromotionRunResponse>> ListPromotionRunsAsync(Guid batchId, CancellationToken cancellationToken);
    Task<BomComparisonRunResponse> CreateBomComparisonAsync(Guid batchId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BomComparisonRunResponse>> ListBomComparisonsAsync(Guid batchId, CancellationToken cancellationToken);
}

public sealed class ImportService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IOntologyService ontologyService,
    IImportFileStorage fileStorage,
    IImportFileParser fileParser,
    IGraphMemoryService graphMemoryService) : IImportService
{
    private const string SuggestionProvider = "deterministic-heuristic-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CreateImportBatchRequestValidator BatchValidator = new();
    private static readonly CreateImportMappingVersionRequestValidator MappingValidator = new();

    public async Task<IReadOnlyCollection<ImportBatchResponse>> ListBatchesAsync(CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.list", ImportPermissions.Read, cancellationToken);
        var batches = await dbContext.ImportBatches
            .AsNoTracking()
            .Include(batch => batch.FileEvidence)
            .Include(batch => batch.MappingVersions)
            .Include(batch => batch.ValidationIssues)
            .Include(batch => batch.StagingRuns)
            .Where(batch => batch.TenantId == context.TenantId)
            .OrderByDescending(batch => batch.CreatedAt)
            .ToListAsync(cancellationToken);
        return batches.Select(ToBatchResponse).ToList();
    }

    public async Task<ImportBatchDetailResponse> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.get", ImportPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.get", cancellationToken);
        return ToBatchDetailResponse(batch);
    }

    public async Task<ImportBatchResponse> CreateBatchAsync(CreateImportBatchRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(BatchValidator, request, cancellationToken);
        var context = await RequireImportPermissionAsync("imports.batches.create", ImportPermissions.Manage, cancellationToken);
        var activePackage = await ontologyService.GetActiveModelPackageAsync(request.ModelPackageKey, cancellationToken)
            ?? throw new RequestValidationException("A published active model package is required before creating an import batch.");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceSystem = NormalizeText(request.SourceSystem),
            NormalizedSourceSystem = NormalizeKey(request.SourceSystem),
            Description = TrimOptional(request.Description),
            Status = ImportBatchStatus.Created,
            ActiveModelPackageVersionId = activePackage.Id,
            ActiveModelPackageKey = activePackage.Key,
            ActiveModelPackageVersionLabel = activePackage.VersionLabel,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "imports.batches.create", $"Import batch for '{batch.SourceSystem}' was created.", nameof(ImportBatch), batch.Id, cancellationToken);
        return ToBatchResponse(batch);
    }

    public async Task<UploadImportFileResponse> UploadFileAsync(Guid batchId, IFormFile file, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.files.upload", ImportPermissions.Manage, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.files.upload", cancellationToken);
        if (file.Length <= 0)
        {
            throw new RequestValidationException("Import file must not be empty.");
        }

        await using var input = file.OpenReadStream();
        var storedFile = await fileStorage.StoreAsync(context.TenantId, batch.Id, file.FileName, input, cancellationToken);
        var audit = await RecordAuditAsync(
            context,
            "imports.files.upload",
            $"Raw import evidence '{Path.GetFileName(file.FileName)}' was uploaded with checksum {storedFile.Sha256Checksum}.",
            nameof(ImportFileEvidence),
            batch.Id,
            cancellationToken);

        var evidence = new ImportFileEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            StorageKey = storedFile.StorageKey,
            Sha256Checksum = storedFile.Sha256Checksum,
            OriginalFileName = NormalizeText(Path.GetFileName(file.FileName)),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "text/csv" : file.ContentType.Trim(),
            SizeBytes = storedFile.SizeBytes,
            UploadedByUserId = context.UserId,
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ImportFileEvidence.Add(evidence);
        batch.Status = ImportBatchStatus.FileUploaded;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!batch.FileEvidence.Any(item => item.Id == evidence.Id))
        {
            batch.FileEvidence.Add(evidence);
        }

        return new UploadImportFileResponse(ToBatchResponse(batch), ToEvidenceResponse(evidence));
    }

    public async Task<ImportPreviewResponse> PreviewMappingAsync(Guid batchId, ImportPreviewRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.mapping.preview", ImportPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.mapping.preview", cancellationToken);
        var evidence = ResolveEvidence(batch, request.EvidenceId);
        var parsed = await ParseEvidenceAsync(evidence, request.SampleRowLimit is <= 0 ? 25 : request.SampleRowLimit, cancellationToken);
        var modelContext = await LoadModelContextAsync(batch.ActiveModelPackageVersionId, context, "imports.mapping.preview", cancellationToken);
        var columnSuggestions = BuildColumnSuggestions(parsed.Headers, modelContext).ToList();
        var lifecycleSuggestions = BuildLifecycleSuggestions(parsed, modelContext, columnSuggestions).ToList();

        return new ImportPreviewResponse(
            batch.Id,
            evidence.Id,
            modelContext.ModelPackage.Id,
            modelContext.ModelPackage.Key,
            modelContext.ModelPackage.VersionLabel,
            SuggestionProvider,
            parsed.Headers,
            parsed.Rows.Take(request.SampleRowLimit is <= 0 ? 25 : request.SampleRowLimit).ToList(),
            columnSuggestions,
            lifecycleSuggestions);
    }

    public async Task<ImportMappingVersionResponse> CreateMappingVersionAsync(CreateImportMappingVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(MappingValidator, request, cancellationToken);
        var context = await RequireImportPermissionAsync("imports.mappings.create", ImportPermissions.Manage, cancellationToken);
        var batch = await RequireBatchAsync(request.ImportBatchId, context, "imports.mappings.create", cancellationToken);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        if (batch.MappingVersions.Any(mapping => mapping.NormalizedVersionLabel == normalizedVersionLabel))
        {
            throw new RequestValidationException("Import mapping version label already exists for this batch.");
        }

        var mapping = new ImportMappingVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            ModelPackageVersionId = batch.ActiveModelPackageVersionId,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            State = ImportMappingState.Draft,
            SuggestionProvider = SuggestionProvider,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            ColumnMappings = request.ColumnMappings.Select(item => new ImportColumnMapping
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                SourceColumn = NormalizeText(item.SourceColumn),
                NormalizedSourceColumn = NormalizeKey(item.SourceColumn),
                CanonicalObjectType = NormalizeText(item.CanonicalObjectType),
                NormalizedCanonicalObjectType = NormalizeKey(item.CanonicalObjectType),
                CanonicalAttributeKey = TrimOptional(item.CanonicalAttributeKey),
                NormalizedCanonicalAttributeKey = TrimOptional(item.CanonicalAttributeKey) is null ? null : NormalizeKey(item.CanonicalAttributeKey!),
                IsIdentityField = item.IsIdentityField,
                IsRequired = item.IsRequired
            }).ToList(),
            LifecycleMappings = request.LifecycleMappings.Select(item => new ImportLifecycleMapping
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                SourceValue = NormalizeText(item.SourceValue),
                NormalizedSourceValue = NormalizeKey(item.SourceValue),
                CanonicalLifecycleKey = NormalizeText(item.CanonicalLifecycleKey),
                NormalizedCanonicalLifecycleKey = NormalizeKey(item.CanonicalLifecycleKey)
            }).ToList()
        };

        dbContext.ImportMappingVersions.Add(mapping);
        batch.Status = ImportBatchStatus.MappingDrafted;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!batch.MappingVersions.Any(item => item.Id == mapping.Id))
        {
            batch.MappingVersions.Add(mapping);
        }

        await RecordAuditAsync(context, "imports.mappings.create", $"Import mapping '{mapping.VersionLabel}' was created as a draft.", nameof(ImportMappingVersion), mapping.Id, cancellationToken);
        return ToMappingResponse(mapping);
    }

    public async Task<ImportMappingVersionResponse> ApproveMappingVersionAsync(Guid mappingVersionId, ApproveImportMappingRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.mappings.approve", ImportPermissions.Approve, cancellationToken);
        var mapping = await RequireMappingAsync(mappingVersionId, context, "imports.mappings.approve", cancellationToken);
        if (mapping.State == ImportMappingState.Approved)
        {
            return ToMappingResponse(mapping);
        }

        if (mapping.State != ImportMappingState.Draft)
        {
            throw new RequestValidationException("Only draft import mappings can be approved.");
        }

        var modelContext = await LoadModelContextAsync(mapping.ModelPackageVersionId, context, "imports.mappings.approve", cancellationToken);
        ValidateMappingAgainstModel(mapping, modelContext);
        mapping.State = ImportMappingState.Approved;
        mapping.ApprovedByUserId = context.UserId;
        mapping.ApprovedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            mapping.Summary = request.Summary.Trim();
        }

        mapping.ImportBatch!.Status = ImportBatchStatus.MappingApproved;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "imports.mappings.approve", $"Import mapping '{mapping.VersionLabel}' was approved.", nameof(ImportMappingVersion), mapping.Id, cancellationToken);
        return ToMappingResponse(mapping);
    }

    public async Task<ImportValidationResponse> ValidateBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.validate", ImportPermissions.Manage, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.validate", cancellationToken);
        var mapping = GetApprovedMapping(batch);
        var modelContext = await LoadModelContextAsync(batch.ActiveModelPackageVersionId, context, "imports.batches.validate", cancellationToken);
        var evidence = ResolveEvidence(batch, null);
        var parsed = await ParseEvidenceAsync(evidence, null, cancellationToken);
        var issues = ValidateParsedRows(batch, mapping, modelContext, parsed).ToList();

        dbContext.ImportValidationIssues.RemoveRange(batch.ValidationIssues);
        batch.ValidationIssues.Clear();
        dbContext.ImportValidationIssues.AddRange(issues);
        batch.ValidatedAt = DateTimeOffset.UtcNow;
        batch.Status = issues.Any(issue => issue.Severity == ImportIssueSeverity.Error)
            ? ImportBatchStatus.Failed
            : ImportBatchStatus.Validated;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "imports.batches.validate", $"Import batch validation completed with {issues.Count} issue(s).", nameof(ImportBatch), batch.Id, cancellationToken);

        return new ImportValidationResponse(
            batch.Id,
            mapping.Id,
            issues.All(issue => issue.Severity != ImportIssueSeverity.Error),
            issues.Count(issue => issue.Severity == ImportIssueSeverity.Error),
            issues.Count(issue => issue.Severity == ImportIssueSeverity.Warning),
            issues.Select(ToIssueResponse).ToList());
    }

    public async Task<ImportStagingGraphRunResponse> StageBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.stage", ImportPermissions.Stage, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.stage", cancellationToken);
        var mapping = GetApprovedMapping(batch);
        var modelContext = await LoadModelContextAsync(batch.ActiveModelPackageVersionId, context, "imports.batches.stage", cancellationToken);
        var evidence = ResolveEvidence(batch, null);
        var parsed = await ParseEvidenceAsync(evidence, null, cancellationToken);
        var issues = ValidateParsedRows(batch, mapping, modelContext, parsed).ToList();
        if (issues.Any(issue => issue.Severity == ImportIssueSeverity.Error))
        {
            dbContext.ImportValidationIssues.RemoveRange(batch.ValidationIssues);
            batch.ValidationIssues.Clear();
            dbContext.ImportValidationIssues.AddRange(issues);
            batch.ValidatedAt = DateTimeOffset.UtcNow;
            batch.Status = ImportBatchStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new RequestValidationException("Import batch has validation errors and cannot be staged.");
        }

        var run = new ImportStagingGraphRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            ImportMappingVersionId = mapping.Id,
            Status = ImportStagingRunStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ImportStagingGraphRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var nodeIds = new List<Guid>();
            var relationshipIds = new List<Guid>();
            var identityMappings = mapping.ColumnMappings.Where(item => item.IsIdentityField).ToList();
            var bomHeaders = TryResolveBomHeaders(parsed.Headers);
            if (bomHeaders is not null)
            {
                foreach (var row in parsed.Rows)
                {
                    var parentId = GetRowValue(row, bomHeaders.ParentHeader);
                    var childId = GetRowValue(row, bomHeaders.ChildHeader);
                    if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
                    {
                        continue;
                    }

                    var parent = await graphMemoryService.CreateNodeAsync(
                        new CreateGraphNodeRequest(
                            context.TenantId,
                            GraphSpace.Staging,
                            "part",
                            TrustState.Unverified,
                            new Dictionary<string, string?> { ["partNumber"] = parentId },
                            new GraphSourceReference(batch.SourceSystem, parentId, batch.Id.ToString())),
                        cancellationToken);
                    var child = await graphMemoryService.CreateNodeAsync(
                        new CreateGraphNodeRequest(
                            context.TenantId,
                            GraphSpace.Staging,
                            "part",
                            TrustState.Unverified,
                            new Dictionary<string, string?> { ["partNumber"] = childId },
                            new GraphSourceReference(batch.SourceSystem, childId, batch.Id.ToString())),
                        cancellationToken);
                    var relationship = await graphMemoryService.CreateRelationshipAsync(
                        new CreateGraphRelationshipRequest(
                            context.TenantId,
                            parent.NodeId,
                            child.NodeId,
                            "BOM_CONTAINS",
                            TrustState.Unverified,
                            BuildBomRelationshipAttributes(row, bomHeaders),
                            new GraphSourceReference(batch.SourceSystem, $"{parentId}|{childId}", batch.Id.ToString())),
                        cancellationToken);
                    nodeIds.Add(parent.NodeId);
                    nodeIds.Add(child.NodeId);
                    relationshipIds.Add(relationship.RelationshipId);
                }
            }
            else
            {
                foreach (var row in parsed.Rows)
                {
                    var objectType = identityMappings.First().CanonicalObjectType;
                    var sourceRecordId = BuildSourceRecordId(row, identityMappings);
                    var attributes = BuildGraphAttributes(row, mapping);
                    attributes["lifecycleState"] = ResolveLifecycleValue(row, mapping);

                    var node = await graphMemoryService.CreateNodeAsync(
                        new CreateGraphNodeRequest(
                            context.TenantId,
                            GraphSpace.Staging,
                            objectType,
                            TrustState.Unverified,
                            attributes,
                            new GraphSourceReference(batch.SourceSystem, sourceRecordId, batch.Id.ToString())),
                        cancellationToken);
                    nodeIds.Add(node.NodeId);
                }
            }

            run.Status = ImportStagingRunStatus.Completed;
            run.NodeCount = nodeIds.Count;
            run.RelationshipCount = relationshipIds.Count;
            run.GraphNodeIdsJson = JsonSerializer.Serialize(nodeIds, JsonOptions);
            run.GraphRelationshipIdsJson = JsonSerializer.Serialize(relationshipIds, JsonOptions);
            run.CompletedAt = DateTimeOffset.UtcNow;
            batch.Status = ImportBatchStatus.Staged;
            batch.StagedAt = run.CompletedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
            await RecordAuditAsync(context, "imports.batches.stage", $"Import batch staged {nodeIds.Count} unverified node(s).", nameof(ImportBatch), batch.Id, cancellationToken);
            return ToStagingRunResponse(run);
        }
        catch (Exception exception) when (exception is not RequestValidationException)
        {
            run.Status = ImportStagingRunStatus.Failed;
            run.FailureSummary = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            batch.Status = ImportBatchStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ImportPromotionRunResponse> PromoteBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.promote", ImportPermissions.Approve, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.promote", cancellationToken);
        var stagingRun = RequireLatestCompletedStagingRun(batch);
        await ValidatePromotionGatesAsync(batch, stagingRun, cancellationToken);

        var run = new ImportPromotionRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            ImportStagingGraphRunId = stagingRun.Id,
            Status = ImportPromotionRunStatus.Running,
            SourceEvidenceIdsJson = JsonSerializer.Serialize(batch.FileEvidence.Select(evidence => evidence.Id).Order().ToList(), JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ImportPromotionRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var copied = await graphMemoryService.PromoteStagingAsync(
                context.TenantId,
                DeserializeGuidArray(stagingRun.GraphNodeIdsJson),
                DeserializeGuidArray(stagingRun.GraphRelationshipIdsJson),
                cancellationToken);
            var audit = await RecordAuditAsync(context, "imports.batches.promote", $"Import batch promoted {copied.TrustedNodeIds.Count} trusted node(s) and {copied.TrustedRelationshipIds.Count} trusted relationship(s).", nameof(ImportBatch), batch.Id, cancellationToken);
            run.Status = ImportPromotionRunStatus.Completed;
            run.PromotedNodeCount = copied.TrustedNodeIds.Count;
            run.PromotedRelationshipCount = copied.TrustedRelationshipIds.Count;
            run.AuditRecordId = audit.Id;
            run.CompletedAt = DateTimeOffset.UtcNow;
            batch.Status = ImportBatchStatus.Promoted;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToPromotionRunResponse(run);
        }
        catch (Exception exception) when (exception is not RequestValidationException)
        {
            run.Status = ImportPromotionRunStatus.Failed;
            run.FailureSummary = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RejectedStagingSummaryResponse> RejectStagingAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.reject_staging", ImportPermissions.Approve, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.reject_staging", cancellationToken);
        var stagingRun = RequireLatestCompletedStagingRun(batch);
        var validationSummary = new
        {
            errorCount = batch.ValidationIssues.Count(issue => issue.Severity == ImportIssueSeverity.Error),
            warningCount = batch.ValidationIssues.Count(issue => issue.Severity == ImportIssueSeverity.Warning),
            issueCodes = batch.ValidationIssues.Select(issue => issue.IssueCode).Distinct().Order().ToList()
        };
        var decisionSummary = await BuildDecisionSummaryAsync(context.TenantId, batch.Id, stagingRun.Id, cancellationToken);
        var audit = await RecordAuditAsync(context, "imports.batches.reject_staging", "Import staging graph was rejected and summarized.", nameof(ImportBatch), batch.Id, cancellationToken);
        var summary = new RejectedStagingSummary
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            ImportStagingGraphRunId = stagingRun.Id,
            ValidationSummaryJson = JsonSerializer.Serialize(validationSummary, JsonOptions),
            DecisionSummaryJson = JsonSerializer.Serialize(decisionSummary, JsonOptions),
            NodeCount = stagingRun.NodeCount,
            RelationshipCount = stagingRun.RelationshipCount,
            SourceEvidenceIdsJson = JsonSerializer.Serialize(batch.FileEvidence.Select(evidence => evidence.Id).Order().ToList(), JsonOptions),
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RejectedStagingSummaries.Add(summary);
        batch.Status = ImportBatchStatus.Rejected;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRejectedStagingSummaryResponse(summary);
    }

    public async Task<IReadOnlyCollection<ImportPromotionRunResponse>> ListPromotionRunsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.promotion_runs.list", ImportPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.promotion_runs.list", cancellationToken);
        return await dbContext.ImportPromotionRuns
            .AsNoTracking()
            .Where(run => run.TenantId == context.TenantId && run.ImportBatchId == batch.Id)
            .OrderByDescending(run => run.CreatedAt)
            .Select(run => ToPromotionRunResponse(run))
            .ToListAsync(cancellationToken);
    }

    public async Task<BomComparisonRunResponse> CreateBomComparisonAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.bom_comparison", ImportPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.bom_comparison", cancellationToken);
        var evidence = ResolveEvidence(batch, null);
        var parsed = await ParseEvidenceAsync(evidence, null, cancellationToken);
        var result = BuildBomComparison(parsed);
        var audit = await RecordAuditAsync(context, "imports.batches.bom_comparison", "CAD BOM and EBOM metadata were compared.", nameof(ImportBatch), batch.Id, cancellationToken);
        var run = new BomComparisonRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            SourceContext = batch.SourceSystem,
            CadSummaryJson = JsonSerializer.Serialize(result.CadSummary, JsonOptions),
            EbomSummaryJson = JsonSerializer.Serialize(result.EbomSummary, JsonOptions),
            MissingInCadCount = result.MissingInCad.Count,
            MissingInEbomCount = result.MissingInEbom.Count,
            QuantityMismatchCount = result.QuantityMismatches.Count,
            UsageReferenceMismatchCount = result.UsageReferenceMismatches.Count,
            UnresolvedIdentityCount = result.UnresolvedIdentity.Count,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.BomComparisonRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToBomComparisonRunResponse(run);
    }

    public async Task<IReadOnlyCollection<BomComparisonRunResponse>> ListBomComparisonsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.batches.bom_comparisons.list", ImportPermissions.Read, cancellationToken);
        var batch = await RequireBatchAsync(batchId, context, "imports.batches.bom_comparisons.list", cancellationToken);
        return await dbContext.BomComparisonRuns
            .AsNoTracking()
            .Where(run => run.TenantId == context.TenantId && run.ImportBatchId == batch.Id)
            .OrderByDescending(run => run.CreatedAt)
            .Select(run => ToBomComparisonRunResponse(run))
            .ToListAsync(cancellationToken);
    }

    private async Task ValidatePromotionGatesAsync(ImportBatch batch, ImportStagingGraphRun stagingRun, CancellationToken cancellationToken)
    {
        if (batch.Status != ImportBatchStatus.Staged)
        {
            throw new RequestValidationException("Only staged import batches can be promoted.");
        }

        _ = GetApprovedMapping(batch);
        if (batch.ValidationIssues.Any(issue => issue.Severity == ImportIssueSeverity.Error))
        {
            throw new RequestValidationException("Import batch has validation errors and cannot be promoted.");
        }

        var blockingQualityCount = await dbContext.DataQualityIssues.CountAsync(
            issue => issue.TenantId == batch.TenantId
                && (issue.ImportBatchId == batch.Id || issue.ImportStagingGraphRunId == stagingRun.Id)
                && (issue.Status == DataQualityIssueStatus.Open || issue.Status == DataQualityIssueStatus.Acknowledged)
                && (issue.Severity == DataQualitySeverity.High || issue.Severity == DataQualitySeverity.Critical),
            cancellationToken);
        if (blockingQualityCount > 0)
        {
            throw new RequestValidationException("Import batch has unresolved blocking data-quality issues and cannot be promoted.");
        }

        var blockingIdentityCount = await dbContext.IdentityCandidateLinks.CountAsync(
            link => link.TenantId == batch.TenantId
                && (link.ImportBatchId == batch.Id || link.ImportStagingGraphRunId == stagingRun.Id)
                && (link.State == IdentityCandidateState.Conflicted || link.State == IdentityCandidateState.Provisional || link.State == IdentityCandidateState.Unverified),
            cancellationToken);
        if (blockingIdentityCount > 0)
        {
            throw new RequestValidationException("Import batch has unresolved identity candidates and cannot be promoted.");
        }
    }

    private async Task<object> BuildDecisionSummaryAsync(Guid tenantId, Guid batchId, Guid stagingRunId, CancellationToken cancellationToken)
    {
        var identityCounts = await dbContext.IdentityCandidateLinks
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && (link.ImportBatchId == batchId || link.ImportStagingGraphRunId == stagingRunId))
            .GroupBy(link => link.State)
            .Select(group => new { state = group.Key.ToString(), count = group.Count() })
            .ToListAsync(cancellationToken);
        var qualityCounts = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(issue => issue.TenantId == tenantId && (issue.ImportBatchId == batchId || issue.ImportStagingGraphRunId == stagingRunId))
            .GroupBy(issue => issue.Status)
            .Select(group => new { status = group.Key.ToString(), count = group.Count() })
            .ToListAsync(cancellationToken);
        return new { identityCounts, dataQualityCounts = qualityCounts };
    }

    private static ImportStagingGraphRun RequireLatestCompletedStagingRun(ImportBatch batch)
    {
        return batch.StagingRuns
            .Where(run => run.Status == ImportStagingRunStatus.Completed)
            .OrderByDescending(run => run.CompletedAt ?? run.CreatedAt)
            .FirstOrDefault()
            ?? throw new RequestValidationException("A completed staging graph run is required.");
    }

    private static BomComparisonResult BuildBomComparison(ParsedImportFile parsed)
    {
        var sideHeader = FindHeader(parsed.Headers, "bomSide", "bom_side", "side", "sourceBom", "source_bom")
            ?? throw new RequestValidationException("BOM comparison requires a CAD/EBOM side column.");
        var parentHeader = FindHeader(parsed.Headers, "parent", "parentPart", "parent_part", "assembly", "assemblyNumber")
            ?? throw new RequestValidationException("BOM comparison requires a parent item column.");
        var childHeader = FindHeader(parsed.Headers, "child", "childPart", "child_part", "component", "componentNumber", "item")
            ?? throw new RequestValidationException("BOM comparison requires a child item column.");
        var quantityHeader = FindHeader(parsed.Headers, "quantity", "qty");
        var unitHeader = FindHeader(parsed.Headers, "unit", "uom");
        var usageHeader = FindHeader(parsed.Headers, "usage", "findNumber", "find_number", "reference", "referenceDesignator", "reference_designator");

        var lines = parsed.Rows.Select((row, index) => ToBomLine(row, index + 2, sideHeader, parentHeader, childHeader, quantityHeader, unitHeader, usageHeader)).ToList();
        var cad = lines.Where(line => line.Side == "CAD").ToDictionary(line => line.Key, StringComparer.OrdinalIgnoreCase);
        var ebom = lines.Where(line => line.Side == "EBOM").ToDictionary(line => line.Key, StringComparer.OrdinalIgnoreCase);
        var missingInCad = ebom.Keys.Except(cad.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var missingInEbom = cad.Keys.Except(ebom.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var quantityMismatches = cad.Keys.Intersect(ebom.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => !string.Equals(cad[key].Quantity, ebom[key].Quantity, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cad[key].Unit, ebom[key].Unit, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var usageReferenceMismatches = cad.Keys.Intersect(ebom.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => !string.Equals(cad[key].UsageReference, ebom[key].UsageReference, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unresolved = lines
            .Where(line => string.IsNullOrWhiteSpace(line.ParentIdentity) || string.IsNullOrWhiteSpace(line.ChildIdentity))
            .Select(line => $"row:{line.RowNumber}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BomComparisonResult(
            new BomSideSummary(cad.Count),
            new BomSideSummary(ebom.Count),
            missingInCad,
            missingInEbom,
            quantityMismatches,
            usageReferenceMismatches,
            unresolved);
    }

    private static BomLine ToBomLine(
        IReadOnlyDictionary<string, string?> row,
        int rowNumber,
        string sideHeader,
        string parentHeader,
        string childHeader,
        string? quantityHeader,
        string? unitHeader,
        string? usageHeader)
    {
        var side = GetRowValue(row, sideHeader);
        var normalizedSide = NormalizeLoose(side);
        var canonicalSide = normalizedSide.Contains("cad", StringComparison.Ordinal) ? "CAD"
            : normalizedSide.Contains("ebom", StringComparison.Ordinal) || normalizedSide.Contains("engineering", StringComparison.Ordinal) ? "EBOM"
            : throw new RequestValidationException("BOM side values must identify CAD or EBOM.");
        var parent = GetRowValue(row, parentHeader);
        var child = GetRowValue(row, childHeader);
        return new BomLine(
            canonicalSide,
            parent,
            child,
            $"{parent}|{child}",
            quantityHeader is null ? null : GetRowValue(row, quantityHeader),
            unitHeader is null ? null : GetRowValue(row, unitHeader),
            usageHeader is null ? null : GetRowValue(row, usageHeader),
            rowNumber);
    }

    private static string? FindHeader(IReadOnlyCollection<string> headers, params string[] candidates)
    {
        var normalizedCandidates = candidates.Select(NormalizeLoose).ToHashSet();
        return headers.FirstOrDefault(header => normalizedCandidates.Contains(NormalizeLoose(header)));
    }

    private static BomHeaders? TryResolveBomHeaders(IReadOnlyCollection<string> headers)
    {
        var parentHeader = FindHeader(headers, "parent", "parentPart", "parent_part", "assembly", "assemblyNumber");
        var childHeader = FindHeader(headers, "child", "childPart", "child_part", "component", "componentNumber", "item");
        if (parentHeader is null || childHeader is null)
        {
            return null;
        }

        return new BomHeaders(
            parentHeader,
            childHeader,
            FindHeader(headers, "quantity", "qty"),
            FindHeader(headers, "unit", "uom"),
            FindHeader(headers, "usage", "findNumber", "find_number", "reference", "referenceDesignator", "reference_designator"));
    }

    private static Dictionary<string, string?> BuildBomRelationshipAttributes(IReadOnlyDictionary<string, string?> row, BomHeaders headers)
    {
        return new Dictionary<string, string?>
        {
            ["quantity"] = headers.QuantityHeader is null ? null : GetRowValue(row, headers.QuantityHeader),
            ["unit"] = headers.UnitHeader is null ? null : GetRowValue(row, headers.UnitHeader),
            ["usageReference"] = headers.UsageHeader is null ? null : GetRowValue(row, headers.UsageHeader)
        };
    }

    private static string GetRowValue(IReadOnlyDictionary<string, string?> row, string header)
    {
        return row.TryGetValue(header, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private async Task<ActiveTenantContext> RequireImportPermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ImportPermissions.Admin, cancellationToken)
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
            throw new TenantAccessDeniedException("User lacks import permission.");
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

    private async Task<ImportMappingVersion> RequireMappingAsync(Guid mappingVersionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.ImportMappingVersions
            .Include(item => item.ImportBatch)
            .Include(item => item.ColumnMappings)
            .Include(item => item.LifecycleMappings)
            .SingleOrDefaultAsync(candidate => candidate.Id == mappingVersionId, cancellationToken)
            ?? throw new RequestValidationException("Import mapping version was not found.");
        await EnsureSameTenantAsync(mapping.TenantId, context, action, "import_tenant_mismatch", "The requested import mapping belongs to a different tenant.", cancellationToken);
        return mapping;
    }

    private async Task EnsureSameTenantAsync(Guid resourceTenantId, ActiveTenantContext context, string action, string reason, string safeSummary, CancellationToken cancellationToken)
    {
        if (resourceTenantId == context.TenantId)
        {
            return;
        }

        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, reason, safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("Import resource is not available in the active tenant.");
    }

    private async Task<ImportModelContext> LoadModelContextAsync(Guid modelPackageVersionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var modelPackage = await dbContext.ModelPackageVersions
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.ObjectTypes)
            .Include(item => item.LifecycleVocabularyVersion)
            .ThenInclude(item => item!.States)
            .Include(item => item.AttributeSchemaVersion)
            .ThenInclude(item => item!.Attributes)
            .SingleOrDefaultAsync(item => item.Id == modelPackageVersionId, cancellationToken)
            ?? throw new RequestValidationException("Referenced model package version was not found.");
        await EnsureSameTenantAsync(modelPackage.TenantId, context, action, "model_package_tenant_mismatch", "The referenced model package belongs to a different tenant.", cancellationToken);
        if (modelPackage.State != OntologyPublicationState.Published
            || modelPackage.OntologyVersion?.State != OntologyPublicationState.Published
            || modelPackage.LifecycleVocabularyVersion?.State != OntologyPublicationState.Published
            || modelPackage.AttributeSchemaVersion?.State != OntologyPublicationState.Published)
        {
            throw new RequestValidationException("Import mappings require a published model package and published model package parts.");
        }

        return new ImportModelContext(modelPackage, modelPackage.OntologyVersion!, modelPackage.LifecycleVocabularyVersion!, modelPackage.AttributeSchemaVersion!);
    }

    private static ImportFileEvidence ResolveEvidence(ImportBatch batch, Guid? evidenceId)
    {
        if (evidenceId is not null)
        {
            return batch.FileEvidence.SingleOrDefault(evidence => evidence.Id == evidenceId.Value)
                ?? throw new RequestValidationException("Import file evidence was not found for this batch.");
        }

        return batch.FileEvidence.OrderByDescending(evidence => evidence.CreatedAt).FirstOrDefault()
            ?? throw new RequestValidationException("Import batch does not have file evidence yet.");
    }

    private async Task<ParsedImportFile> ParseEvidenceAsync(ImportFileEvidence evidence, int? maxRows, CancellationToken cancellationToken)
    {
        await using var stream = await fileStorage.OpenReadAsync(evidence.StorageKey, cancellationToken);
        return await fileParser.ParseAsync(evidence.OriginalFileName, stream, maxRows, cancellationToken);
    }

    private static IEnumerable<ImportColumnMappingSuggestionResponse> BuildColumnSuggestions(
        IReadOnlyCollection<string> headers,
        ImportModelContext modelContext)
    {
        var attributes = modelContext.AttributeSchema.Attributes.ToList();
        var firstObjectType = modelContext.Ontology.ObjectTypes.OrderBy(item => item.Key).First();
        foreach (var header in headers)
        {
            var normalizedHeader = NormalizeLoose(header);
            var attribute = attributes.FirstOrDefault(item => NormalizeLoose(item.AttributeKey) == normalizedHeader)
                ?? attributes.FirstOrDefault(item => NormalizeLoose(item.DisplayName ?? item.AttributeKey) == normalizedHeader)
                ?? attributes.FirstOrDefault(item => normalizedHeader.Contains(NormalizeLoose(item.AttributeKey), StringComparison.Ordinal));
            var objectType = attribute?.AppliesToObjectType ?? firstObjectType.Key;
            var isIdentity = normalizedHeader.Contains("id", StringComparison.Ordinal)
                || normalizedHeader.Contains("number", StringComparison.Ordinal)
                || normalizedHeader.EndsWith("no", StringComparison.Ordinal);
            yield return new ImportColumnMappingSuggestionResponse(
                header,
                objectType,
                attribute?.AttributeKey,
                isIdentity,
                attribute?.IsRequired ?? isIdentity,
                attribute is null ? 0.45m : 0.85m,
                attribute is null ? "Column matched to the first canonical object type by heuristic fallback." : "Column matched by canonical attribute name.");
        }
    }

    private static IEnumerable<ImportLifecycleMappingSuggestionResponse> BuildLifecycleSuggestions(
        ParsedImportFile parsed,
        ImportModelContext modelContext,
        IReadOnlyCollection<ImportColumnMappingSuggestionResponse> columnSuggestions)
    {
        var lifecycleColumn = parsed.Headers.FirstOrDefault(header => NormalizeLoose(header).Contains("lifecycle", StringComparison.Ordinal))
            ?? parsed.Headers.FirstOrDefault(header => NormalizeLoose(header).Contains("status", StringComparison.Ordinal))
            ?? parsed.Headers.FirstOrDefault(header => NormalizeLoose(header).Contains("state", StringComparison.Ordinal));
        if (lifecycleColumn is null)
        {
            yield break;
        }

        var states = modelContext.LifecycleVocabulary.States.ToList();
        var sourceValues = parsed.Rows
            .Select(row => row.TryGetValue(lifecycleColumn, out var value) ? value : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceValue in sourceValues)
        {
            var normalizedSource = NormalizeLoose(sourceValue);
            var state = states.FirstOrDefault(item => NormalizeLoose(item.Key) == normalizedSource)
                ?? states.FirstOrDefault(item => NormalizeLoose(item.DisplayName) == normalizedSource)
                ?? states.FirstOrDefault(item => normalizedSource.Contains(NormalizeLoose(item.Key), StringComparison.Ordinal));
            if (state is not null)
            {
                yield return new ImportLifecycleMappingSuggestionResponse(sourceValue, state.Key, 0.85m, "Lifecycle value matched by canonical state key or display name.");
            }
        }
    }

    private static void ValidateMappingAgainstModel(ImportMappingVersion mapping, ImportModelContext modelContext)
    {
        var objectTypes = modelContext.Ontology.ObjectTypes.Select(item => item.NormalizedKey).ToHashSet();
        var attributeKeys = modelContext.AttributeSchema.Attributes
            .Select(item => $"{item.NormalizedAppliesToObjectType}:{item.NormalizedAttributeKey}")
            .ToHashSet();
        var lifecycleStates = modelContext.LifecycleVocabulary.States.Select(item => item.NormalizedKey).ToHashSet();
        if (!mapping.ColumnMappings.Any(item => item.IsIdentityField))
        {
            throw new RequestValidationException("Approved import mappings require at least one identity field mapping.");
        }

        foreach (var columnMapping in mapping.ColumnMappings)
        {
            if (!objectTypes.Contains(columnMapping.NormalizedCanonicalObjectType))
            {
                throw new RequestValidationException($"Unknown canonical object type '{columnMapping.CanonicalObjectType}'.");
            }

            if (columnMapping.NormalizedCanonicalAttributeKey is not null
                && !attributeKeys.Contains($"{columnMapping.NormalizedCanonicalObjectType}:{columnMapping.NormalizedCanonicalAttributeKey}"))
            {
                throw new RequestValidationException($"Unknown canonical attribute '{columnMapping.CanonicalAttributeKey}' for object type '{columnMapping.CanonicalObjectType}'.");
            }
        }

        foreach (var lifecycleMapping in mapping.LifecycleMappings)
        {
            if (!lifecycleStates.Contains(lifecycleMapping.NormalizedCanonicalLifecycleKey))
            {
                throw new RequestValidationException($"Unknown canonical lifecycle state '{lifecycleMapping.CanonicalLifecycleKey}'.");
            }
        }
    }

    private static IEnumerable<ImportValidationIssue> ValidateParsedRows(
        ImportBatch batch,
        ImportMappingVersion mapping,
        ImportModelContext modelContext,
        ParsedImportFile parsed)
    {
        ValidateMappingAgainstModel(mapping, modelContext);
        var headerKeys = parsed.Headers.Select(NormalizeKey).ToHashSet();
        foreach (var columnMapping in mapping.ColumnMappings)
        {
            if (!headerKeys.Contains(columnMapping.NormalizedSourceColumn))
            {
                yield return NewIssue(batch, mapping, ImportIssueSeverity.Error, null, columnMapping.SourceColumn, columnMapping.CanonicalObjectType, "missing_source_column", $"Source column '{columnMapping.SourceColumn}' is missing from the import file.");
            }
        }

        var rowNumber = 1;
        foreach (var row in parsed.Rows)
        {
            rowNumber++;
            foreach (var columnMapping in mapping.ColumnMappings.Where(item => item.IsRequired || item.IsIdentityField))
            {
                if (!row.TryGetValue(columnMapping.SourceColumn, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    yield return NewIssue(batch, mapping, ImportIssueSeverity.Error, rowNumber, columnMapping.SourceColumn, columnMapping.CanonicalObjectType, "missing_required_value", $"Required source column '{columnMapping.SourceColumn}' is empty.");
                }
            }

            foreach (var columnMapping in mapping.ColumnMappings.Where(item => item.NormalizedCanonicalAttributeKey is not null))
            {
                if (!row.TryGetValue(columnMapping.SourceColumn, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var attribute = modelContext.AttributeSchema.Attributes.Single(item =>
                    item.NormalizedAppliesToObjectType == columnMapping.NormalizedCanonicalObjectType
                    && item.NormalizedAttributeKey == columnMapping.NormalizedCanonicalAttributeKey);
                if (!IsValueValid(attribute.ValueType, value))
                {
                    yield return NewIssue(batch, mapping, ImportIssueSeverity.Error, rowNumber, columnMapping.SourceColumn, columnMapping.CanonicalObjectType, "invalid_value_type", $"Value in '{columnMapping.SourceColumn}' is not a valid {attribute.ValueType}.");
                }
            }

            var lifecycleValue = ResolveLifecycleValue(row, mapping);
            if (lifecycleValue is null)
            {
                yield return NewIssue(batch, mapping, ImportIssueSeverity.Error, rowNumber, null, null, "invalid_lifecycle_value", "No mapped canonical lifecycle value was present for this row.");
            }
        }
    }

    private static ImportValidationIssue NewIssue(
        ImportBatch batch,
        ImportMappingVersion mapping,
        ImportIssueSeverity severity,
        int? rowNumber,
        string? sourceColumn,
        string? canonicalObjectType,
        string issueCode,
        string message)
    {
        return new ImportValidationIssue
        {
            Id = Guid.NewGuid(),
            TenantId = batch.TenantId,
            ImportBatchId = batch.Id,
            ImportMappingVersionId = mapping.Id,
            Severity = severity,
            RowNumber = rowNumber,
            SourceColumn = sourceColumn,
            CanonicalObjectType = canonicalObjectType,
            IssueCode = issueCode,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static bool IsValueValid(AttributeValueType valueType, string value)
    {
        return valueType switch
        {
            AttributeValueType.Text => true,
            AttributeValueType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            AttributeValueType.Integer => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            AttributeValueType.Boolean => bool.TryParse(value, out _),
            AttributeValueType.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
            AttributeValueType.DateTime => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
            AttributeValueType.Json => IsJson(value),
            _ => false
        };
    }

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ImportMappingVersion GetApprovedMapping(ImportBatch batch)
    {
        return batch.MappingVersions
            .Where(mapping => mapping.State == ImportMappingState.Approved)
            .OrderByDescending(mapping => mapping.ApprovedAt)
            .FirstOrDefault()
            ?? throw new RequestValidationException("An approved import mapping is required before validation or staging.");
    }

    private static string BuildSourceRecordId(IReadOnlyDictionary<string, string?> row, IReadOnlyCollection<ImportColumnMapping> identityMappings)
    {
        return string.Join("|", identityMappings.Select(mapping => row.TryGetValue(mapping.SourceColumn, out var value) ? value : null).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static Dictionary<string, string?> BuildGraphAttributes(IReadOnlyDictionary<string, string?> row, ImportMappingVersion mapping)
    {
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var columnMapping in mapping.ColumnMappings.Where(item => item.CanonicalAttributeKey is not null))
        {
            attributes[columnMapping.CanonicalAttributeKey!] = row.TryGetValue(columnMapping.SourceColumn, out var value) ? value : null;
        }

        return attributes;
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

    private async Task<AuditRecordResponse> RecordAuditAsync(ActiveTenantContext context, string action, string safeSummary, string sourceObjectType, Guid sourceObjectId, CancellationToken cancellationToken)
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

    private static ImportBatchDetailResponse ToBatchDetailResponse(ImportBatch batch)
    {
        return new ImportBatchDetailResponse(
            ToBatchResponse(batch),
            batch.FileEvidence.OrderByDescending(item => item.CreatedAt).Select(ToEvidenceResponse).ToList(),
            batch.MappingVersions.OrderByDescending(item => item.CreatedAt).Select(ToMappingResponse).ToList(),
            batch.ValidationIssues.OrderByDescending(item => item.CreatedAt).Select(ToIssueResponse).ToList(),
            batch.StagingRuns.OrderByDescending(item => item.CreatedAt).Select(ToStagingRunResponse).ToList());
    }

    private static ImportBatchResponse ToBatchResponse(ImportBatch batch)
    {
        return new ImportBatchResponse(
            batch.Id,
            batch.TenantId,
            batch.SourceSystem,
            batch.Description,
            batch.Status,
            batch.ActiveModelPackageVersionId,
            batch.ActiveModelPackageKey,
            batch.ActiveModelPackageVersionLabel,
            batch.FileEvidence.Count,
            batch.MappingVersions.Count,
            batch.ValidationIssues.Count,
            batch.StagingRuns.Count,
            batch.CreatedByUserId,
            batch.CreatedAt,
            batch.ValidatedAt,
            batch.StagedAt);
    }

    private static ImportFileEvidenceResponse ToEvidenceResponse(ImportFileEvidence evidence)
    {
        return new ImportFileEvidenceResponse(
            evidence.Id,
            evidence.TenantId,
            evidence.ImportBatchId,
            evidence.StorageKey,
            evidence.Sha256Checksum,
            evidence.OriginalFileName,
            evidence.ContentType,
            evidence.SizeBytes,
            evidence.UploadedByUserId,
            evidence.AuditRecordId,
            evidence.CreatedAt);
    }

    private static ImportMappingVersionResponse ToMappingResponse(ImportMappingVersion mapping)
    {
        return new ImportMappingVersionResponse(
            mapping.Id,
            mapping.TenantId,
            mapping.ImportBatchId,
            mapping.ModelPackageVersionId,
            mapping.VersionLabel,
            mapping.Summary,
            mapping.State,
            mapping.SuggestionProvider,
            mapping.ColumnMappings.Count,
            mapping.LifecycleMappings.Count,
            mapping.CreatedByUserId,
            mapping.CreatedAt,
            mapping.ApprovedByUserId,
            mapping.ApprovedAt,
            mapping.RejectedByUserId,
            mapping.RejectedAt,
            mapping.ColumnMappings.OrderBy(item => item.SourceColumn).Select(item => new ImportColumnMappingResponse(item.Id, item.SourceColumn, item.CanonicalObjectType, item.CanonicalAttributeKey, item.IsIdentityField, item.IsRequired)).ToList(),
            mapping.LifecycleMappings.OrderBy(item => item.SourceValue).Select(item => new ImportLifecycleMappingResponse(item.Id, item.SourceValue, item.CanonicalLifecycleKey)).ToList());
    }

    private static ImportValidationIssueResponse ToIssueResponse(ImportValidationIssue issue)
    {
        return new ImportValidationIssueResponse(
            issue.Id,
            issue.TenantId,
            issue.ImportBatchId,
            issue.ImportMappingVersionId,
            issue.Severity,
            issue.RowNumber,
            issue.SourceColumn,
            issue.CanonicalObjectType,
            issue.IssueCode,
            issue.Message,
            issue.CreatedAt);
    }

    private static ImportStagingGraphRunResponse ToStagingRunResponse(ImportStagingGraphRun run)
    {
        return new ImportStagingGraphRunResponse(
            run.Id,
            run.TenantId,
            run.ImportBatchId,
            run.ImportMappingVersionId,
            run.Status,
            run.NodeCount,
            run.RelationshipCount,
            DeserializeGuidArray(run.GraphNodeIdsJson),
            DeserializeGuidArray(run.GraphRelationshipIdsJson),
            run.FailureSummary,
            run.CreatedAt,
            run.CompletedAt);
    }

    private static ImportPromotionRunResponse ToPromotionRunResponse(ImportPromotionRun run)
    {
        return new ImportPromotionRunResponse(
            run.Id,
            run.TenantId,
            run.ImportBatchId,
            run.ImportStagingGraphRunId,
            run.Status,
            run.PromotedNodeCount,
            run.PromotedRelationshipCount,
            DeserializeGuidArray(run.SourceEvidenceIdsJson),
            run.AuditRecordId,
            run.FailureSummary,
            run.CreatedAt,
            run.CompletedAt);
    }

    private static RejectedStagingSummaryResponse ToRejectedStagingSummaryResponse(RejectedStagingSummary summary)
    {
        return new RejectedStagingSummaryResponse(
            summary.Id,
            summary.TenantId,
            summary.ImportBatchId,
            summary.ImportStagingGraphRunId,
            summary.ValidationSummaryJson,
            summary.DecisionSummaryJson,
            summary.NodeCount,
            summary.RelationshipCount,
            DeserializeGuidArray(summary.SourceEvidenceIdsJson),
            summary.AuditRecordId,
            summary.CreatedAt);
    }

    private static BomComparisonRunResponse ToBomComparisonRunResponse(BomComparisonRun run)
    {
        return new BomComparisonRunResponse(
            run.Id,
            run.TenantId,
            run.ImportBatchId,
            run.SourceContext,
            run.CadSummaryJson,
            run.EbomSummaryJson,
            run.MissingInCadCount,
            run.MissingInEbomCount,
            run.QuantityMismatchCount,
            run.UsageReferenceMismatchCount,
            run.UnresolvedIdentityCount,
            run.ResultJson,
            run.AuditRecordId,
            run.CreatedAt);
    }

    private static IReadOnlyCollection<Guid> DeserializeGuidArray(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyCollection<Guid>>(json, JsonOptions) ?? [];
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
    private static string NormalizeLoose(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BomHeaders(
        string ParentHeader,
        string ChildHeader,
        string? QuantityHeader,
        string? UnitHeader,
        string? UsageHeader);

    private sealed record BomLine(
        string Side,
        string ParentIdentity,
        string ChildIdentity,
        string Key,
        string? Quantity,
        string? Unit,
        string? UsageReference,
        int RowNumber);

    private sealed record BomSideSummary(int LineCount);

    private sealed record BomComparisonResult(
        BomSideSummary CadSummary,
        BomSideSummary EbomSummary,
        IReadOnlyCollection<string> MissingInCad,
        IReadOnlyCollection<string> MissingInEbom,
        IReadOnlyCollection<string> QuantityMismatches,
        IReadOnlyCollection<string> UsageReferenceMismatches,
        IReadOnlyCollection<string> UnresolvedIdentity);

    private sealed class CreateImportBatchRequestValidator : AbstractValidator<CreateImportBatchRequest>
    {
        public CreateImportBatchRequestValidator()
        {
            RuleFor(request => request.SourceSystem).NotEmpty().MaximumLength(120);
            RuleFor(request => request.Description).MaximumLength(1000);
            RuleFor(request => request.ModelPackageKey).MaximumLength(120);
        }
    }

    private sealed class CreateImportMappingVersionRequestValidator : AbstractValidator<CreateImportMappingVersionRequest>
    {
        public CreateImportMappingVersionRequestValidator()
        {
            RuleFor(request => request.ImportBatchId).NotEmpty();
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.ColumnMappings).NotEmpty();
            RuleForEach(request => request.ColumnMappings).ChildRules(item =>
            {
                item.RuleFor(value => value.SourceColumn).NotEmpty().MaximumLength(160);
                item.RuleFor(value => value.CanonicalObjectType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.CanonicalAttributeKey).MaximumLength(160);
            });
            RuleForEach(request => request.LifecycleMappings).ChildRules(item =>
            {
                item.RuleFor(value => value.SourceValue).NotEmpty().MaximumLength(160);
                item.RuleFor(value => value.CanonicalLifecycleKey).NotEmpty().MaximumLength(120);
            });
        }
    }
}
