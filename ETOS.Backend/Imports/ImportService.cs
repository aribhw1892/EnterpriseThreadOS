using System.Globalization;
using System.Text.Json;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Ontology;
using ETOS.Backend.Recommendations;
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
    Task<ImportMappingVersionResponse> RejectMappingVersionAsync(Guid mappingVersionId, RejectImportMappingRequest request, CancellationToken cancellationToken);
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
    IModelPackageContextResolver modelPackageContextResolver,
    IMappingSuggestionProviderSelector mappingSuggestionProviderSelector,
    IImportMappingLearningSignalEmitter learningSignalEmitter,
    IImportFileStorage fileStorage,
    IImportFileParser fileParser,
    IGraphMemoryService graphMemoryService,
    IRecommendationFactory recommendationFactory) : IImportService
{
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
        var sampleRowLimit = request.SampleRowLimit is <= 0 ? 25 : request.SampleRowLimit;
        var suggestions = await mappingSuggestionProviderSelector.SuggestAsync(
            new ImportMappingSuggestionRequest(
                parsed.Headers,
                parsed.Rows.Take(sampleRowLimit).ToList(),
                modelContext.Resolved,
                request.SuggestionProviderKey,
                request.IncludeDiagnostics,
                request.MappingAssistantAgentKey,
                request.MappingAssistantAgentVersionId),
            cancellationToken);

        return new ImportPreviewResponse(
            batch.Id,
            evidence.Id,
            modelContext.ModelPackage.Id,
            modelContext.ModelPackage.Key,
            modelContext.ModelPackage.VersionLabel,
            suggestions.ProviderKey,
            parsed.Headers,
            parsed.Rows.Take(sampleRowLimit).ToList(),
            suggestions.ColumnSuggestions,
            suggestions.LifecycleSuggestions,
            MapDiagnostics(suggestions.Diagnostics));
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

        var modelContext = await LoadModelContextAsync(batch.ActiveModelPackageVersionId, context, "imports.mappings.create", cancellationToken);
        StructuralRelationshipResolver.ValidateStructuralRelationshipType(modelContext.Resolved, request.StructuralRelationshipType);
        var evidence = ResolveEvidence(batch, null);
        var parsed = await ParseEvidenceAsync(evidence, 25, cancellationToken);
        var previewSuggestions = await ResolveLearningSuggestionsAsync(
            parsed.Headers,
            parsed.Rows.ToList(),
            modelContext.Resolved,
            cancellationToken);

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
            SuggestionProvider = previewSuggestions.ProviderKey,
            StructuralRelationshipType = TrimOptional(request.StructuralRelationshipType),
            NormalizedStructuralRelationshipType = TrimOptional(request.StructuralRelationshipType) is null ? null : NormalizeKey(request.StructuralRelationshipType!),
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

        var audit = await RecordAuditAsync(context, "imports.mappings.create", $"Import mapping '{mapping.VersionLabel}' was created as a draft.", nameof(ImportMappingVersion), mapping.Id, cancellationToken);
        await learningSignalEmitter.EmitCorrectedAsync(context, mapping, previewSuggestions, audit.Id, cancellationToken);
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
        if (!string.IsNullOrWhiteSpace(request.StructuralRelationshipType))
        {
            mapping.StructuralRelationshipType = NormalizeText(request.StructuralRelationshipType);
            mapping.NormalizedStructuralRelationshipType = NormalizeKey(request.StructuralRelationshipType);
        }

        ValidateMappingAgainstModel(mapping, modelContext);
        if (!string.IsNullOrWhiteSpace(mapping.StructuralRelationshipType))
        {
            ValidateStructuralRelationshipEndpoints(mapping, modelContext);
        }

        mapping.State = ImportMappingState.Approved;
        mapping.ApprovedByUserId = context.UserId;
        mapping.ApprovedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            mapping.Summary = request.Summary.Trim();
        }

        mapping.ImportBatch!.Status = ImportBatchStatus.MappingApproved;
        await dbContext.SaveChangesAsync(cancellationToken);
        var audit = await RecordAuditAsync(context, "imports.mappings.approve", $"Import mapping '{mapping.VersionLabel}' was approved.", nameof(ImportMappingVersion), mapping.Id, cancellationToken);
        await learningSignalEmitter.EmitApprovedAsync(context, mapping, null, audit.Id, cancellationToken);
        return ToMappingResponse(mapping);
    }

    public async Task<ImportMappingVersionResponse> RejectMappingVersionAsync(Guid mappingVersionId, RejectImportMappingRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireImportPermissionAsync("imports.mappings.reject", ImportPermissions.Approve, cancellationToken);
        var mapping = await RequireMappingAsync(mappingVersionId, context, "imports.mappings.reject", cancellationToken);
        if (mapping.State == ImportMappingState.Rejected)
        {
            return ToMappingResponse(mapping);
        }

        if (mapping.State != ImportMappingState.Draft)
        {
            throw new RequestValidationException("Only draft import mappings can be rejected.");
        }

        mapping.State = ImportMappingState.Rejected;
        mapping.RejectedByUserId = context.UserId;
        mapping.RejectedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            mapping.Summary = request.Summary.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var audit = await RecordAuditAsync(context, "imports.mappings.reject", $"Import mapping '{mapping.VersionLabel}' was rejected.", nameof(ImportMappingVersion), mapping.Id, cancellationToken);
        await learningSignalEmitter.EmitRejectedAsync(context, mapping, request.Reason, audit.Id, cancellationToken);
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
            var stagingIssues = new List<ImportValidationIssue>();
            var identityMappings = mapping.ColumnMappings.Where(item => item.IsIdentityField).ToList();
            var structuralHeaders = ImportStructuralImportHelper.TryResolveStructuralHeaders(parsed.Headers, modelContext.ImportProfile);
            if (structuralHeaders is not null)
            {
                var structuralRelationship = StructuralRelationshipResolver.Resolve(modelContext.Resolved, mapping, structuralHeaders);
                var relationshipGraphType = modelContext.Resolved.ResolveGraphRelationshipType(structuralRelationship.RelationshipType);
                var rowNumber = 1;
                foreach (var row in parsed.Rows)
                {
                    rowNumber++;
                    var parentId = ImportStructuralImportHelper.GetRowValue(row, structuralHeaders.ParentHeader);
                    var childId = ImportStructuralImportHelper.GetRowValue(row, structuralHeaders.ChildHeader);
                    if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
                    {
                        continue;
                    }

                    var parentIdentityAttributes = ImportStructuralImportHelper.BuildStructuralIdentityAttributes(
                        parentId,
                        structuralHeaders.ParentHeader,
                        structuralRelationship.ParentObjectType,
                        mapping,
                        modelContext.Resolved);
                    var childIdentityAttributes = ImportStructuralImportHelper.BuildStructuralIdentityAttributes(
                        childId,
                        structuralHeaders.ChildHeader,
                        structuralRelationship.ChildObjectType,
                        mapping,
                        modelContext.Resolved);
                    var parentIdentityKey = GraphIdentityKeyBuilder.Build(
                        batch.SourceSystem,
                        structuralRelationship.ParentObjectType,
                        parentIdentityAttributes);
                    var childIdentityKey = GraphIdentityKeyBuilder.Build(
                        batch.SourceSystem,
                        structuralRelationship.ChildObjectType,
                        childIdentityAttributes);
                    var parent = parentIdentityKey is null
                        ? null
                        : await graphMemoryService.FindNodeByIdentityAsync(
                            context.TenantId,
                            GraphSpace.Staging,
                            parentIdentityKey,
                            cancellationToken);
                    var child = childIdentityKey is null
                        ? null
                        : await graphMemoryService.FindNodeByIdentityAsync(
                            context.TenantId,
                            GraphSpace.Staging,
                            childIdentityKey,
                            cancellationToken);
                    if (parent is null || child is null)
                    {
                        stagingIssues.Add(NewIssue(
                            batch,
                            mapping,
                            ImportIssueSeverity.Warning,
                            rowNumber,
                            parent is null ? structuralHeaders.ParentHeader : structuralHeaders.ChildHeader,
                            parent is null ? structuralRelationship.ParentObjectType : structuralRelationship.ChildObjectType,
                            "structural-endpoint-missing",
                            parent is null && child is null
                                ? $"Structural relationship row references missing parent '{parentId}' and child '{childId}' objects in staging."
                                : parent is null
                                    ? $"Structural relationship row references missing parent object '{parentId}' in staging."
                                    : $"Structural relationship row references missing child object '{childId}' in staging."));
                        continue;
                    }

                    var relationshipAttributes = structuralRelationship.BomRelationship is not null
                        ? ImportStructuralImportHelper.BuildRelationshipAttributes(row, structuralHeaders, structuralRelationship.BomRelationship)
                        : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    var relationship = await graphMemoryService.EnsureRelationshipAsync(
                        new CreateGraphRelationshipRequest(
                            context.TenantId,
                            parent.NodeId,
                            child.NodeId,
                            relationshipGraphType,
                            TrustState.Unverified,
                            relationshipAttributes,
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
                    var lifecycleValue = ImportFlatMetadataHelper.ResolveLifecycleValue(row, mapping);
                    if (!string.IsNullOrWhiteSpace(lifecycleValue))
                    {
                        attributes["lifecycleState"] = lifecycleValue;
                    }

                    var identityAttributes = BuildFlatIdentityAttributes(row, identityMappings);
                    var identityKey = GraphIdentityKeyBuilder.Build(batch.SourceSystem, objectType, identityAttributes);
                    var node = await graphMemoryService.EnsureNodeAsync(
                        new CreateGraphNodeRequest(
                            context.TenantId,
                            GraphSpace.Staging,
                            objectType,
                            TrustState.Unverified,
                            attributes,
                            new GraphSourceReference(batch.SourceSystem, sourceRecordId, batch.Id.ToString()),
                            identityKey),
                        cancellationToken);
                    nodeIds.Add(node.NodeId);
                }
            }

            if (stagingIssues.Count > 0)
            {
                dbContext.ImportValidationIssues.AddRange(stagingIssues);
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
            run.FailureSummary = exception.Message;
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
            run.FailureSummary = exception.Message;
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
        var modelContext = await LoadModelContextAsync(batch.ActiveModelPackageVersionId, context, "imports.batches.bom_comparison", cancellationToken);
        var evidence = ResolveEvidence(batch, null);
        var parsed = await ParseEvidenceAsync(evidence, null, cancellationToken);
        var result = BuildBomComparison(parsed, modelContext);
        var auditSummary = modelContext.ImportProfile.RecommendationTemplates?.StructuralComparisonAuditSummary
            ?? "Structural comparison completed using the active model package.";
        var audit = await RecordAuditAsync(context, "imports.batches.bom_comparison", auditSummary, nameof(ImportBatch), batch.Id, cancellationToken);
        var run = new BomComparisonRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportBatchId = batch.Id,
            SourceContext = batch.SourceSystem,
            CadSummaryJson = JsonSerializer.Serialize(result.PrimarySideSummary, JsonOptions),
            EbomSummaryJson = JsonSerializer.Serialize(result.SecondarySideSummary, JsonOptions),
            MissingInPrimarySideCount = result.MissingInPrimary.Count,
            MissingInSecondarySideCount = result.MissingInSecondary.Count,
            QuantityMismatchCount = result.QuantityMismatches.Count,
            UsageReferenceMismatchCount = result.UsageReferenceMismatches.Count,
            UnresolvedIdentityCount = result.UnresolvedIdentity.Count,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            AuditRecordId = audit.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.BomComparisonRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (run.MissingInSecondarySideCount + run.QuantityMismatchCount + run.UsageReferenceMismatchCount > 0)
        {
            try
            {
                await recommendationFactory.FromBomComparisonRunForImportAsync(run.Id, context, cancellationToken);
            }
            catch (RequestValidationException)
            {
                // Best-effort auto-recommendation; BOM comparison remains successful.
            }
        }

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

    private static BomComparisonResult BuildBomComparison(ParsedImportFile parsed, ImportModelContext modelContext)
    {
        var comparison = ImportStructuralImportHelper.BuildStructuralComparison(parsed, modelContext.ImportProfile);
        return new BomComparisonResult(
            new BomSideSummary(comparison.PrimarySide.LineCount),
            new BomSideSummary(comparison.SecondarySide.LineCount),
            comparison.MissingInPrimary,
            comparison.MissingInSecondary,
            comparison.QuantityMismatches,
            comparison.UsageReferenceMismatches,
            comparison.UnresolvedIdentities);
    }

    private async Task<ImportModelContext> LoadModelContextAsync(Guid modelPackageVersionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var resolved = await modelPackageContextResolver.ResolvePublishedAsync(modelPackageVersionId, context, action, cancellationToken);
        return new ImportModelContext(resolved);
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

    private async Task<ParsedImportFile> ParseEvidenceAsync(ImportFileEvidence evidence, int? maxRows, CancellationToken cancellationToken)
    {
        await using var stream = await fileStorage.OpenReadAsync(evidence.StorageKey, cancellationToken);
        return await fileParser.ParseAsync(evidence.OriginalFileName, stream, maxRows, cancellationToken);
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

        StructuralRelationshipResolver.ValidateStructuralRelationshipType(modelContext.Resolved, mapping.StructuralRelationshipType);
    }

    private static void ValidateStructuralRelationshipEndpoints(ImportMappingVersion mapping, ImportModelContext modelContext)
    {
        var profile = modelContext.ImportProfile;
        var parentMapping = mapping.ColumnMappings.FirstOrDefault(item =>
            item.IsIdentityField
            && profile.ParentColumnSynonyms.Any(synonym =>
                string.Equals(synonym, item.SourceColumn, StringComparison.OrdinalIgnoreCase)));
        var childMapping = mapping.ColumnMappings.FirstOrDefault(item =>
            item.IsIdentityField
            && profile.ChildColumnSynonyms.Any(synonym =>
                string.Equals(synonym, item.SourceColumn, StringComparison.OrdinalIgnoreCase)));
        if (parentMapping is null || childMapping is null)
        {
            return;
        }

        var headers = new ImportStructuralImportHelper.StructuralHeaders(
            parentMapping.SourceColumn,
            childMapping.SourceColumn,
            null,
            null,
            null);
        StructuralRelationshipResolver.Resolve(modelContext.Resolved, mapping, headers);
    }

    private static IEnumerable<ImportValidationIssue> ValidateParsedRows(
        ImportBatch batch,
        ImportMappingVersion mapping,
        ImportModelContext modelContext,
        ParsedImportFile parsed)
    {
        ValidateMappingAgainstModel(mapping, modelContext);
        var structuralHeaders = ImportStructuralImportHelper.TryResolveStructuralHeaders(parsed.Headers, modelContext.ImportProfile);
        var isStructuralImport = structuralHeaders is not null;
        if (isStructuralImport && structuralHeaders is not null)
        {
            foreach (var issue in ValidateStructuralMappingHeaders(batch, mapping, structuralHeaders))
            {
                yield return issue;
            }
        }

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
                    continue;
                }

                if (IsSuspiciousNumericValue(attribute.ValueType, value))
                {
                    yield return NewIssue(batch, mapping, ImportIssueSeverity.Warning, rowNumber, columnMapping.SourceColumn, columnMapping.CanonicalObjectType, "suspicious_numeric_value", $"Value in '{columnMapping.SourceColumn}' is negative and should be reviewed.");
                }
            }

            if (!isStructuralImport)
            {
                var objectType = ImportFlatMetadataHelper.ResolveFlatImportObjectType(mapping);
                foreach (var metadataKey in ImportFlatMetadataHelper.ResolveRequiredMetadataKeys(modelContext.ImportProfile, objectType))
                {
                    var resolvedMetadata = ImportFlatMetadataHelper.ResolveMetadataValue(metadataKey, row, mapping, objectType);
                    if (!string.IsNullOrWhiteSpace(resolvedMetadata))
                    {
                        continue;
                    }

                    if (string.Equals(metadataKey, "lifecycleState", StringComparison.OrdinalIgnoreCase)
                        && ImportFlatMetadataHelper.HasUnmappedLifecycleSourceSignal(row, mapping))
                    {
                        yield return NewIssue(batch, mapping, ImportIssueSeverity.Error, rowNumber, null, objectType, "invalid_lifecycle_value", "No mapped canonical lifecycle value was present for this row.");
                        continue;
                    }

                    yield return NewIssue(
                        batch,
                        mapping,
                        ImportIssueSeverity.Error,
                        rowNumber,
                        null,
                        objectType,
                        "missing_required_metadata",
                        $"Required metadata '{metadataKey}' is missing for object type '{objectType}'.");
                }
            }
        }
    }

    private static IEnumerable<ImportValidationIssue> ValidateStructuralMappingHeaders(
        ImportBatch batch,
        ImportMappingVersion mapping,
        ImportStructuralImportHelper.StructuralHeaders structuralHeaders)
    {
        if (!mapping.ColumnMappings.Any(item =>
                item.IsIdentityField
                && string.Equals(item.SourceColumn, structuralHeaders.ParentHeader, StringComparison.OrdinalIgnoreCase)))
        {
            yield return NewIssue(
                batch,
                mapping,
                ImportIssueSeverity.Error,
                null,
                structuralHeaders.ParentHeader,
                null,
                "missing_structural_identity_mapping",
                $"Structural import requires an identity mapping for parent column '{structuralHeaders.ParentHeader}'.");
        }

        if (!mapping.ColumnMappings.Any(item =>
                item.IsIdentityField
                && string.Equals(item.SourceColumn, structuralHeaders.ChildHeader, StringComparison.OrdinalIgnoreCase)))
        {
            yield return NewIssue(
                batch,
                mapping,
                ImportIssueSeverity.Error,
                null,
                structuralHeaders.ChildHeader,
                null,
                "missing_structural_identity_mapping",
                $"Structural import requires an identity mapping for child column '{structuralHeaders.ChildHeader}'.");
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

    private static bool IsSuspiciousNumericValue(AttributeValueType valueType, string value)
    {
        return valueType == AttributeValueType.Number
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            && parsed < 0;
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

    private static Dictionary<string, string?> BuildFlatIdentityAttributes(
        IReadOnlyDictionary<string, string?> row,
        IReadOnlyCollection<ImportColumnMapping> identityMappings)
    {
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in identityMappings)
        {
            if (mapping.CanonicalAttributeKey is null)
            {
                continue;
            }

            attributes[mapping.CanonicalAttributeKey] = row.TryGetValue(mapping.SourceColumn, out var value) ? value : null;
        }

        return attributes;
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
            mapping.StructuralRelationshipType,
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
            run.MissingInPrimarySideCount,
            run.MissingInSecondarySideCount,
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

    private async Task<ImportMappingSuggestionResult> ResolveLearningSuggestionsAsync(
        IReadOnlyCollection<string> headers,
        IReadOnlyCollection<IReadOnlyDictionary<string, string?>> sampleRows,
        ResolvedModelPackageContext modelContext,
        CancellationToken cancellationToken)
    {
        var suggestionRequest = new ImportMappingSuggestionRequest(headers, sampleRows, modelContext);
        try
        {
            return await mappingSuggestionProviderSelector.SuggestAsync(suggestionRequest, cancellationToken);
        }
        catch (RequestValidationException)
        {
            return await mappingSuggestionProviderSelector.SuggestAsync(
                suggestionRequest with { RequestedProviderKey = MappingSuggestionProviderKeys.RuleBased },
                cancellationToken);
        }
    }

    private static string NormalizeText(string value) => value.Trim();
    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeLoose(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BomSideSummary(int LineCount);

    private sealed record BomComparisonResult(
        BomSideSummary PrimarySideSummary,
        BomSideSummary SecondarySideSummary,
        IReadOnlyCollection<string> MissingInPrimary,
        IReadOnlyCollection<string> MissingInSecondary,
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

    private static ImportMappingSuggestionDiagnosticsResponse? MapDiagnostics(ImportMappingSuggestionDiagnostics? diagnostics)
    {
        if (diagnostics is null)
        {
            return null;
        }

        return new ImportMappingSuggestionDiagnosticsResponse(
            diagnostics.ProviderKey,
            diagnostics.ResolvedAgentKey,
            diagnostics.RuntimeCalled,
            diagnostics.RuntimeAdapterKey,
            diagnostics.RuntimeStatus,
            diagnostics.ModelUsed,
            diagnostics.FallbackAppliedJson,
            diagnostics.TraceNotes,
            diagnostics.PrefetchAttempted,
            diagnostics.PrefetchSucceeded,
            diagnostics.PrefetchToolKey,
            diagnostics.PrefetchToolRunId,
            diagnostics.PrefetchStatus,
            diagnostics.PrefetchError,
            diagnostics.PrefetchToolOutputJson,
            diagnostics.GovernedContextJson,
            diagnostics.StructuredInputJson,
            diagnostics.ToolOutputSummariesJson,
            diagnostics.PromptTemplateBody,
            diagnostics.OutputSchemaJson,
            diagnostics.PrimaryModelProviderKey,
            diagnostics.PrimaryModelId,
            diagnostics.RuntimeStructuredOutputJson,
            diagnostics.UsedRuleBasedFallback,
            diagnostics.ErrorMessage);
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
