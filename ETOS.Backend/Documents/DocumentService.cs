using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Governance;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Documents;

public interface IDocumentService
{
    Task<IReadOnlyCollection<DocumentArtifactSummaryResponse>> ListDocumentsAsync(string? policyKey, CancellationToken cancellationToken);
    Task<DocumentArtifactDetailResponse> GetDocumentAsync(Guid documentId, string? policyKey, CancellationToken cancellationToken);
    Task<DocumentArtifactDetailResponse> CreateDocumentAsync(CreateDocumentArtifactRequest request, CancellationToken cancellationToken);
    Task<DocumentVersionResponse> AddVersionAsync(Guid documentId, IFormFile file, CreateDocumentVersionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DocumentObjectLinkResponse>> ListLinksAsync(Guid documentId, CancellationToken cancellationToken);
    Task<DocumentObjectLinkResponse> CreateLinkAsync(Guid documentId, CreateDocumentObjectLinkRequest request, CancellationToken cancellationToken);
    Task<DataQualityIssueResponse> CreateExtractionIssueAsync(Guid documentId, Guid versionId, CreateDocumentExtractionIssueRequest request, CancellationToken cancellationToken);
    Task<DocumentVectorIndexRecordResponse> RequestVectorIndexAsync(Guid documentId, Guid versionId, CreateDocumentVectorIndexRequest request, CancellationToken cancellationToken);
    Task<CadParsingPlaceholderResponse> GetCadParsingStatusAsync(CancellationToken cancellationToken);
}

public sealed class DocumentService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IDocumentFileStorage fileStorage,
    IDocumentVectorIndexingService vectorIndexingService,
    ICadParsingPlaceholder cadParsingPlaceholder,
    IGraphMemoryService graphMemoryService,
    IClassificationPolicyService classificationPolicyService) : IDocumentService
{
    private const decimal UncertainLinkThreshold = 0.75m;

    public async Task<IReadOnlyCollection<DocumentArtifactSummaryResponse>> ListDocumentsAsync(string? policyKey, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.list", DocumentPermissions.Read, cancellationToken);
        var documents = await dbContext.DocumentArtifacts
            .AsNoTracking()
            .Include(document => document.Versions)
            .Include(document => document.ObjectLinks)
            .Where(document => document.TenantId == context.TenantId)
            .OrderByDescending(document => document.UpdatedAt)
            .ToListAsync(cancellationToken);

        var allowedIds = await EvaluateAllowedDocumentIdsAsync(context, documents, policyKey, "documents.list.policy", cancellationToken);
        return documents
            .Where(document => allowedIds.Contains(document.Id))
            .Select(ToSummaryResponse)
            .ToList();
    }

    public async Task<DocumentArtifactDetailResponse> GetDocumentAsync(Guid documentId, string? policyKey, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.get", DocumentPermissions.Read, cancellationToken);
        var document = await LoadDocumentAsync(documentId, context, "documents.get", cancellationToken);
        var allowedIds = await EvaluateAllowedDocumentIdsAsync(context, [document], policyKey, "documents.get.policy", cancellationToken);
        if (!allowedIds.Contains(document.Id))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "documents.get",
                "permission_denied",
                "Restricted document metadata was denied by classification policy.",
                cancellationToken);
            throw new TenantAccessDeniedException("Document is restricted by policy.");
        }

        return ToDetailResponse(document);
    }

    public async Task<DocumentArtifactDetailResponse> CreateDocumentAsync(CreateDocumentArtifactRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.create", DocumentPermissions.Manage, cancellationToken);
        var documentType = RequireText(request.DocumentType, "Document type is required.", 120);
        var classificationKey = RequireText(request.ClassificationKey, "Classification key is required.", 120);
        var title = RequireText(request.Title, "Document title is required.", 200);
        var ownerUserId = request.OwnerUserId ?? context.UserId;
        if (!await dbContext.Users.AnyAsync(user => user.Id == ownerUserId, cancellationToken))
        {
            throw new RequestValidationException("Document owner user was not found.");
        }

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = "document",
            NormalizedArtifactType = Normalize("document"),
            Name = title,
            Description = TrimOptional(request.Description, 1000),
            OwnerUserId = ownerUserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var document = new DocumentArtifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            Artifact = artifact,
            DocumentType = documentType,
            NormalizedDocumentType = Normalize(documentType),
            ClassificationKey = classificationKey,
            NormalizedClassificationKey = Normalize(classificationKey),
            Title = title,
            Description = TrimOptional(request.Description, 1000),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.DocumentArtifacts.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "documents.create", $"Document '{document.Title}' was created.", nameof(DocumentArtifact), document.Id, cancellationToken);

        return ToDetailResponse(document);
    }

    public async Task<DocumentVersionResponse> AddVersionAsync(Guid documentId, IFormFile file, CreateDocumentVersionRequest request, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new RequestValidationException("Document version upload requires a non-empty file.");
        }

        var context = await RequireDocumentPermissionAsync("documents.versions.create", DocumentPermissions.Manage, cancellationToken);
        var document = await LoadDocumentAsync(documentId, context, "documents.versions.create", cancellationToken);
        var versionLabel = RequireText(request.VersionLabel, "Version label is required.", 80);
        var normalizedVersionLabel = Normalize(versionLabel);
        if (document.Versions.Any(version => version.NormalizedVersionLabel == normalizedVersionLabel))
        {
            throw new RequestValidationException("Document version label already exists.");
        }

        await using var stream = file.OpenReadStream();
        var stored = await fileStorage.StoreAsync(context.TenantId, document.Id, file.FileName, stream, cancellationToken);
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DocumentArtifactId = document.Id,
            VersionLabel = versionLabel,
            NormalizedVersionLabel = normalizedVersionLabel,
            StorageKey = stored.StorageKey,
            Sha256Checksum = stored.Sha256Checksum,
            OriginalFileName = TrimToMax(Path.GetFileName(file.FileName), 260),
            ContentType = TrimToMax(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, 160),
            SizeBytes = stored.SizeBytes,
            ExtractedMetadataSummaryJson = TrimOptional(request.ExtractedMetadataSummaryJson, 4000),
            ExtractionStatus = request.ExtractionStatus,
            ExtractionFailureSummary = TrimOptional(request.ExtractionFailureSummary, 1000),
            UploadedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        document.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.DocumentVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        var audit = await RecordAuditAsync(context, "documents.versions.create", $"Document version '{version.VersionLabel}' was uploaded.", nameof(DocumentVersion), version.Id, cancellationToken);
        version.AuditRecordId = audit.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (version.ExtractionStatus == DocumentExtractionStatus.Failed)
        {
            await CreateExtractionIssueInternalAsync(context, document, version, "Document extraction failed", "document_extraction_failed", version.ExtractionFailureSummary ?? "Document extraction failed.", null, cancellationToken);
        }

        return ToVersionResponse(version);
    }

    public async Task<IReadOnlyCollection<DocumentObjectLinkResponse>> ListLinksAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.links.list", DocumentPermissions.Read, cancellationToken);
        await RequireDocumentExistsAsync(documentId, context, "documents.links.list", cancellationToken);

        return await dbContext.DocumentObjectLinks
            .AsNoTracking()
            .Where(link => link.TenantId == context.TenantId && link.DocumentArtifactId == documentId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => ToLinkResponse(link))
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentObjectLinkResponse> CreateLinkAsync(Guid documentId, CreateDocumentObjectLinkRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.links.create", DocumentPermissions.Link, cancellationToken);
        var document = await LoadDocumentAsync(documentId, context, "documents.links.create", cancellationToken);
        var version = document.Versions.SingleOrDefault(candidate => candidate.Id == request.DocumentVersionId);
        if (version is null)
        {
            throw new RequestValidationException("Document version was not found for the document.");
        }

        if (request.GraphNodeId is null && request.ImportBatchId is null)
        {
            throw new RequestValidationException("Document link requires GraphNodeId or ImportBatchId.");
        }

        if (request.GraphNodeId is not null)
        {
            var node = await graphMemoryService.GetNodeAsync(context.TenantId, request.GraphNodeId.Value, cancellationToken);
            if (node is null)
            {
                throw new RequestValidationException("Graph node was not found for the active tenant.");
            }
        }

        if (request.ImportBatchId is not null)
        {
            var batchExists = await dbContext.ImportBatches
                .AsNoTracking()
                .AnyAsync(batch => batch.TenantId == context.TenantId && batch.Id == request.ImportBatchId.Value, cancellationToken);
            if (!batchExists)
            {
                throw new RequestValidationException("Import batch was not found for the active tenant.");
            }
        }

        var link = new DocumentObjectLink
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DocumentArtifactId = document.Id,
            DocumentVersionId = version.Id,
            GraphNodeId = request.GraphNodeId,
            ImportBatchId = request.ImportBatchId,
            ConfidenceScore = NormalizeConfidence(request.ConfidenceScore),
            EvidenceSummary = RequireText(request.EvidenceSummary, "Evidence summary is required.", 1000),
            ExtractionStatus = request.ExtractionStatus,
            SourceSystem = TrimOptional(request.SourceSystem, 120),
            SourceRecordId = TrimOptional(request.SourceRecordId, 200),
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.DocumentObjectLinks.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        var audit = await RecordAuditAsync(context, "documents.links.create", "Document was linked to enterprise context.", nameof(DocumentObjectLink), link.Id, cancellationToken);
        link.AuditRecordId = audit.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (link.ConfidenceScore < UncertainLinkThreshold || link.ExtractionStatus == DocumentExtractionStatus.Uncertain)
        {
            await CreateLinkIssueInternalAsync(context, document, version, link, cancellationToken);
        }

        return ToLinkResponse(link);
    }

    public async Task<DataQualityIssueResponse> CreateExtractionIssueAsync(Guid documentId, Guid versionId, CreateDocumentExtractionIssueRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.extraction_issues.create", DocumentPermissions.Manage, cancellationToken);
        var document = await LoadDocumentAsync(documentId, context, "documents.extraction_issues.create", cancellationToken);
        var version = document.Versions.SingleOrDefault(candidate => candidate.Id == versionId);
        if (version is null)
        {
            throw new RequestValidationException("Document version was not found for the document.");
        }

        return await CreateExtractionIssueInternalAsync(
            context,
            document,
            version,
            RequireText(request.Title, "Issue title is required.", 200),
            RequireText(request.IssueCode, "Issue code is required.", 120),
            RequireText(request.EvidenceSummary, "Evidence summary is required.", 1000),
            request.Rationale,
            cancellationToken);
    }

    public async Task<DocumentVectorIndexRecordResponse> RequestVectorIndexAsync(Guid documentId, Guid versionId, CreateDocumentVectorIndexRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.vector_index.create", DocumentPermissions.Index, cancellationToken);
        var document = await LoadDocumentAsync(documentId, context, "documents.vector_index.create", cancellationToken);
        var version = document.Versions.SingleOrDefault(candidate => candidate.Id == versionId);
        if (version is null)
        {
            throw new RequestValidationException("Document version was not found for the document.");
        }

        var allowedIds = await EvaluateAllowedDocumentIdsAsync(context, [document], request.PolicyKey, "documents.vector_index.policy", cancellationToken);
        if (!allowedIds.Contains(document.Id))
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, "documents.vector_index.create", "permission_denied", "Restricted document was denied before vector indexing.", cancellationToken);
            throw new TenantAccessDeniedException("Document is restricted by policy.");
        }

        var status = await vectorIndexingService.RequestIndexAsync(version, cancellationToken);
        var record = new DocumentVectorIndexRecord
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DocumentArtifactId = document.Id,
            DocumentVersionId = version.Id,
            ProviderName = "disabled-qdrant-placeholder",
            Status = status,
            TenantFilter = context.TenantId.ToString(),
            PolicyFilterSummary = $"classification={document.ClassificationKey};documentType={document.DocumentType};policy={request.PolicyKey ?? "active"}",
            SafeSummary = TrimOptional(request.SafeSummary, 1000) ?? $"Document '{document.Title}' version '{version.VersionLabel}' is eligible for future vector indexing.",
            FailureSummary = status == DocumentVectorIndexStatus.DisabledPlaceholder ? "Qdrant indexing provider is not enabled in this slice." : null,
            RequestedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.DocumentVectorIndexRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        var audit = await RecordAuditAsync(context, "documents.vector_index.create", "Document vector indexing request was recorded.", nameof(DocumentVectorIndexRecord), record.Id, cancellationToken);
        record.AuditRecordId = audit.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToVectorResponse(record);
    }

    public async Task<CadParsingPlaceholderResponse> GetCadParsingStatusAsync(CancellationToken cancellationToken)
    {
        var context = await RequireDocumentPermissionAsync("documents.cad_parsing.status", DocumentPermissions.Read, cancellationToken);
        await RecordAuditAsync(context, "documents.cad_parsing.status", "Native CAD geometry parsing placeholder was inspected.", "CadParsingPlaceholder", Guid.Empty, cancellationToken);
        return cadParsingPlaceholder.GetStatus();
    }

    private async Task<HashSet<Guid>> EvaluateAllowedDocumentIdsAsync(
        ActiveTenantContext context,
        IReadOnlyCollection<DocumentArtifact> documents,
        string? policyKey,
        string action,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return documents.Select(document => document.Id).ToHashSet();
        }

        var request = new EvaluatePolicyRequest(
            action,
            policyKey,
            documents.Select(document => new PolicyEvaluationContextItem(
                document.Id.ToString(),
                document.DocumentType,
                document.ClassificationKey,
                null,
                document.Id.ToString(),
                $"Document '{document.Title}' metadata.")).ToList());
        var evaluation = await classificationPolicyService.EvaluateAsync(request, cancellationToken);
        return evaluation.AllowedContext
            .Select(item => Guid.TryParse(item.ContextId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
    }

    private async Task<DataQualityIssueResponse> CreateExtractionIssueInternalAsync(
        ActiveTenantContext context,
        DocumentArtifact document,
        DocumentVersion version,
        string title,
        string issueCode,
        string evidenceSummary,
        string? rationale,
        CancellationToken cancellationToken)
    {
        var issue = new DataQualityIssue
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Title = TrimToMax(title, 200),
            IssueCode = TrimToMax(issueCode, 120),
            NormalizedIssueCode = Normalize(issueCode),
            Severity = DataQualitySeverity.Medium,
            Status = DataQualityIssueStatus.Open,
            Origin = DataQualityIssueOrigin.DocumentExtraction,
            AffectedEntityType = DataQualityAffectedEntityType.DocumentVersion,
            TrustImpactPenalty = 0.15m,
            ResultingTrustState = TrustState.Unverified,
            ExcludedFromTrustedRecommendations = true,
            ReviewPriority = DataQualityReviewPriority.High,
            ReviewTaskReady = true,
            ReviewTaskHint = "Review document extraction before using this document as trusted context.",
            ReviewHookCreatedAt = DateTimeOffset.UtcNow,
            UniqueSourceKey = $"document-version:{version.Id}:extraction",
            EvidenceSummary = TrimToMax(evidenceSummary, 1000),
            Rationale = TrimOptional(rationale, 1000),
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        AddIssueSource(issue, context.TenantId, DataQualitySourceLinkType.DocumentArtifact, document.Id.ToString(), "Document", $"Document '{document.Title}' extraction issue source.");
        AddIssueSource(issue, context.TenantId, DataQualitySourceLinkType.DocumentVersion, version.Id.ToString(), "Document version", $"Document version '{version.VersionLabel}' extraction issue source.");

        dbContext.DataQualityIssues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "documents.extraction_issues.create", "Document extraction issue was created.", nameof(DataQualityIssue), issue.Id, cancellationToken);

        return DataQualityIssueService.ToIssueResponse(issue);
    }

    private async Task CreateLinkIssueInternalAsync(
        ActiveTenantContext context,
        DocumentArtifact document,
        DocumentVersion version,
        DocumentObjectLink link,
        CancellationToken cancellationToken)
    {
        var issue = new DataQualityIssue
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Title = "Uncertain document link",
            IssueCode = "document_link_uncertain",
            NormalizedIssueCode = Normalize("document_link_uncertain"),
            Severity = DataQualitySeverity.Medium,
            Status = DataQualityIssueStatus.Open,
            Origin = DataQualityIssueOrigin.DocumentExtraction,
            AffectedEntityType = DataQualityAffectedEntityType.DocumentObjectLink,
            ImportBatchId = link.ImportBatchId,
            GraphNodeId = link.GraphNodeId,
            TrustImpactPenalty = 0.10m,
            ResultingTrustState = TrustState.Provisional,
            ExcludedFromTrustedRecommendations = true,
            ReviewPriority = DataQualityReviewPriority.Normal,
            ReviewTaskReady = true,
            ReviewTaskHint = "Review uncertain document-object link evidence.",
            ReviewHookCreatedAt = DateTimeOffset.UtcNow,
            UniqueSourceKey = $"document-link:{link.Id}",
            EvidenceSummary = $"Document link confidence {(link.ConfidenceScore * 100m):0.0}% requires review.",
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        AddIssueSource(issue, context.TenantId, DataQualitySourceLinkType.DocumentArtifact, document.Id.ToString(), "Document", $"Document '{document.Title}' uncertain link source.");
        AddIssueSource(issue, context.TenantId, DataQualitySourceLinkType.DocumentVersion, version.Id.ToString(), "Document version", $"Document version '{version.VersionLabel}' uncertain link source.");
        AddIssueSource(issue, context.TenantId, DataQualitySourceLinkType.DocumentObjectLink, link.Id.ToString(), "Document link", link.EvidenceSummary);

        dbContext.DataQualityIssues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "documents.links.issue.create", "Uncertain document-object link issue was created.", nameof(DataQualityIssue), issue.Id, cancellationToken);
    }

    private async Task<DocumentArtifact> LoadDocumentAsync(Guid documentId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var document = await dbContext.DocumentArtifacts
            .Include(item => item.Versions)
                .ThenInclude(version => version.VectorIndexRecords)
            .Include(item => item.ObjectLinks)
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken);

        if (document is null)
        {
            throw new RequestValidationException("Document was not found.");
        }

        if (document.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", "Document belongs to a different tenant.", cancellationToken);
            throw new TenantAccessDeniedException("Document belongs to a different tenant.");
        }

        return document;
    }

    private async Task RequireDocumentExistsAsync(Guid documentId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        _ = await LoadDocumentAsync(documentId, context, action, cancellationToken);
    }

    private async Task<ActiveTenantContext> RequireDocumentPermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, DocumentPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "permission_denied", $"The user lacks the {permissionKey} permission.", cancellationToken);
            throw new TenantAccessDeniedException("User lacks document permission.");
        }

        return context;
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
                sourceObjectType,
                sourceObjectId == Guid.Empty ? null : sourceObjectId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational,
                IsArchiveEligible: true),
            cancellationToken);
    }

    private static void AddIssueSource(DataQualityIssue issue, Guid tenantId, DataQualitySourceLinkType sourceType, string sourceId, string label, string safeSummary)
    {
        issue.SourceLinks.Add(new DataQualityIssueSourceLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DataQualityIssueId = issue.Id,
            SourceType = sourceType,
            SourceId = sourceId,
            Label = label,
            SafeSummary = TrimToMax(safeSummary, 1000),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static DocumentArtifactSummaryResponse ToSummaryResponse(DocumentArtifact document)
    {
        var latestVersion = document.Versions.OrderByDescending(version => version.CreatedAt).FirstOrDefault();
        return new DocumentArtifactSummaryResponse(
            document.Id,
            document.TenantId,
            document.ArtifactId,
            document.DocumentType,
            document.ClassificationKey,
            document.Title,
            document.Description,
            document.OwnerUserId,
            latestVersion is null ? null : ToVersionResponse(latestVersion),
            document.ObjectLinks.Count,
            document.CreatedAt,
            document.UpdatedAt);
    }

    private static DocumentArtifactDetailResponse ToDetailResponse(DocumentArtifact document)
    {
        return new DocumentArtifactDetailResponse(
            document.Id,
            document.TenantId,
            document.ArtifactId,
            document.DocumentType,
            document.ClassificationKey,
            document.Title,
            document.Description,
            document.OwnerUserId,
            document.Versions.OrderByDescending(version => version.CreatedAt).Select(ToVersionResponse).ToList(),
            document.ObjectLinks.OrderByDescending(link => link.CreatedAt).Select(ToLinkResponse).ToList(),
            document.Versions.SelectMany(version => version.VectorIndexRecords).OrderByDescending(record => record.CreatedAt).Select(ToVectorResponse).ToList(),
            document.CreatedAt,
            document.UpdatedAt);
    }

    private static DocumentVersionResponse ToVersionResponse(DocumentVersion version)
    {
        return new DocumentVersionResponse(
            version.Id,
            version.TenantId,
            version.DocumentArtifactId,
            version.VersionLabel,
            version.StorageKey,
            version.Sha256Checksum,
            version.OriginalFileName,
            version.ContentType,
            version.SizeBytes,
            version.ExtractedMetadataSummaryJson,
            version.ExtractionStatus,
            version.ExtractionFailureSummary,
            version.UploadedByUserId,
            version.AuditRecordId,
            version.CreatedAt);
    }

    private static DocumentObjectLinkResponse ToLinkResponse(DocumentObjectLink link)
    {
        return new DocumentObjectLinkResponse(
            link.Id,
            link.TenantId,
            link.DocumentArtifactId,
            link.DocumentVersionId,
            link.GraphNodeId,
            link.ImportBatchId,
            link.ConfidenceScore,
            link.EvidenceSummary,
            link.ExtractionStatus,
            link.SourceSystem,
            link.SourceRecordId,
            link.CreatedByUserId,
            link.AuditRecordId,
            link.CreatedAt);
    }

    private static DocumentVectorIndexRecordResponse ToVectorResponse(DocumentVectorIndexRecord record)
    {
        return new DocumentVectorIndexRecordResponse(
            record.Id,
            record.TenantId,
            record.DocumentArtifactId,
            record.DocumentVersionId,
            record.ProviderName,
            record.Status,
            record.TenantFilter,
            record.PolicyFilterSummary,
            record.SafeSummary,
            record.FailureSummary,
            record.RequestedByUserId,
            record.AuditRecordId,
            record.CreatedAt);
    }

    private static decimal NormalizeConfidence(decimal confidence)
    {
        if (confidence < 0m || confidence > 1m)
        {
            throw new RequestValidationException("Confidence score must be between 0 and 1.");
        }

        return confidence;
    }

    private static string RequireText(string? value, string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(message);
        }

        return TrimToMax(value, maxLength);
    }

    private static string TrimToMax(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimOptional(string? value, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : TrimToMax(value, maxLength);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
