using System.Globalization;
using System.Text.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
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
