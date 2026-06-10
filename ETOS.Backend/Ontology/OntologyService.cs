using System.Text.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tenancy;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Ontology;

public interface IOntologyService
{
    Task<IReadOnlyCollection<OntologyVersionResponse>> ListOntologyVersionsAsync(CancellationToken cancellationToken);
    Task<OntologyVersionDetailResponse> GetOntologyVersionAsync(Guid versionId, CancellationToken cancellationToken);
    Task<OntologyVersionResponse> CreateOntologyVersionAsync(CreateOntologyVersionRequest request, CancellationToken cancellationToken);
    Task<OntologyVersionResponse> PublishOntologyVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SemanticLayerVersionResponse>> ListSemanticLayerVersionsAsync(CancellationToken cancellationToken);
    Task<SemanticLayerVersionResponse> CreateSemanticLayerVersionAsync(CreateSemanticLayerVersionRequest request, CancellationToken cancellationToken);
    Task<SemanticLayerVersionResponse> PublishSemanticLayerVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LifecycleVocabularyVersionResponse>> ListLifecycleVocabularyVersionsAsync(CancellationToken cancellationToken);
    Task<LifecycleVocabularyVersionDetailResponse> GetLifecycleVocabularyVersionAsync(Guid versionId, CancellationToken cancellationToken);
    Task<LifecycleVocabularyVersionResponse> CreateLifecycleVocabularyVersionAsync(CreateLifecycleVocabularyVersionRequest request, CancellationToken cancellationToken);
    Task<LifecycleVocabularyVersionResponse> PublishLifecycleVocabularyVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AttributeSchemaVersionResponse>> ListAttributeSchemaVersionsAsync(CancellationToken cancellationToken);
    Task<AttributeSchemaVersionDetailResponse> GetAttributeSchemaVersionAsync(Guid versionId, CancellationToken cancellationToken);
    Task<AttributeSchemaVersionResponse> CreateAttributeSchemaVersionAsync(CreateAttributeSchemaVersionRequest request, CancellationToken cancellationToken);
    Task<AttributeSchemaVersionResponse> PublishAttributeSchemaVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModelPackageVersionResponse>> ListModelPackageVersionsAsync(CancellationToken cancellationToken);
    Task<ModelPackageVersionResponse> GetModelPackageVersionAsync(Guid versionId, CancellationToken cancellationToken);
    Task<ModelPackagePreviewResponse> PreviewModelPackageAsync(ModelPackagePreviewRequest request, CancellationToken cancellationToken);
    Task<ModelPackageVersionResponse> CreateModelPackageVersionAsync(CreateModelPackageVersionRequest request, CancellationToken cancellationToken);
    Task<ModelPackageVersionResponse> PublishModelPackageVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken);
    Task<ModelPackageVersionResponse?> GetActiveModelPackageAsync(string? key, CancellationToken cancellationToken);
}

public sealed class OntologyService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder) : IOntologyService
{
    private static readonly CreateOntologyVersionRequestValidator CreateOntologyValidator = new();
    private static readonly CreateSemanticLayerVersionRequestValidator CreateSemanticLayerValidator = new();
    private static readonly CreateLifecycleVocabularyVersionRequestValidator CreateLifecycleValidator = new();
    private static readonly CreateAttributeSchemaVersionRequestValidator CreateAttributeSchemaValidator = new();
    private static readonly CreateModelPackageVersionRequestValidator CreateModelPackageValidator = new();

    public async Task<IReadOnlyCollection<OntologyVersionResponse>> ListOntologyVersionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.versions.list", OntologyPermissions.Read, cancellationToken);
        var versions = await dbContext.OntologyVersions
            .AsNoTracking()
            .Include(version => version.ObjectTypes)
            .Include(version => version.RelationshipTypes)
            .Include(version => version.BomRelationships)
            .Where(version => version.TenantId == context.TenantId)
            .OrderBy(version => version.Key)
            .ThenByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);

        return versions.Select(ToOntologyVersionResponse).ToList();
    }

    public async Task<OntologyVersionDetailResponse> GetOntologyVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.versions.get", OntologyPermissions.Read, cancellationToken);
        var version = await RequireOntologyVersionAsync(versionId, context, "ontology.versions.get", cancellationToken);
        return ToOntologyVersionDetailResponse(version);
    }

    public async Task<OntologyVersionResponse> CreateOntologyVersionAsync(CreateOntologyVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateOntologyValidator, request, cancellationToken);
        var context = await RequireOntologyPermissionAsync("ontology.versions.create", OntologyPermissions.Manage, cancellationToken);
        ValidateOntologyDefinition(request);
        var normalizedKey = NormalizeKey(request.Key);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        await EnsureVersionLabelAvailableAsync(dbContext.OntologyVersions, context.TenantId, normalizedKey, normalizedVersionLabel, "Ontology version label already exists for this key.", cancellationToken);

        var version = new OntologyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Key = NormalizeText(request.Key),
            NormalizedKey = normalizedKey,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            State = OntologyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            ObjectTypes = request.ObjectTypes.Select(item => new OntologyObjectTypeDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                Key = NormalizeText(item.Key),
                NormalizedKey = NormalizeKey(item.Key),
                DisplayName = NormalizeText(item.DisplayName),
                Description = TrimOptional(item.Description),
                VersionIdentityFieldsJson = TrimOptional(item.VersionIdentityFieldsJson),
                SafeSummary = NormalizeText(item.SafeSummary),
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList(),
            RelationshipTypes = request.RelationshipTypes.Select(item => new SemanticRelationshipDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                RelationshipType = NormalizeText(item.RelationshipType),
                NormalizedRelationshipType = NormalizeKey(item.RelationshipType),
                FromObjectType = NormalizeText(item.FromObjectType),
                NormalizedFromObjectType = NormalizeKey(item.FromObjectType),
                ToObjectType = NormalizeText(item.ToObjectType),
                NormalizedToObjectType = NormalizeKey(item.ToObjectType),
                Description = TrimOptional(item.Description),
                IsVersionRelationship = item.IsVersionRelationship,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList(),
            BomRelationships = request.BomRelationships.Select(item => new BomRelationshipDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                RelationshipType = NormalizeText(item.RelationshipType),
                NormalizedRelationshipType = NormalizeKey(item.RelationshipType),
                ParentObjectType = NormalizeText(item.ParentObjectType),
                NormalizedParentObjectType = NormalizeKey(item.ParentObjectType),
                ChildObjectType = NormalizeText(item.ChildObjectType),
                NormalizedChildObjectType = NormalizeKey(item.ChildObjectType),
                QuantityAttributeKey = TrimOptional(item.QuantityAttributeKey),
                UnitAttributeKey = TrimOptional(item.UnitAttributeKey),
                FindNumberAttributeKey = TrimOptional(item.FindNumberAttributeKey),
                ReferenceDesignatorAttributeKey = TrimOptional(item.ReferenceDesignatorAttributeKey),
                LifecycleConstraintJson = TrimOptional(item.LifecycleConstraintJson),
                RequiresApproval = item.RequiresApproval,
                AuditReferenceAttributeKey = TrimOptional(item.AuditReferenceAttributeKey),
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList()
        };

        dbContext.OntologyVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "ontology.versions.create", $"Ontology version '{version.Key}' '{version.VersionLabel}' was created.", nameof(OntologyVersion), version.Id, cancellationToken);
        return ToOntologyVersionResponse(version);
    }

    public async Task<OntologyVersionResponse> PublishOntologyVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.versions.publish", OntologyPermissions.Publish, cancellationToken);
        var version = await RequireOntologyVersionAsync(versionId, context, "ontology.versions.publish", cancellationToken);
        ValidateOntologyVersionReady(version);
        await PublishVersionGroupAsync(
            dbContext.OntologyVersions.Where(candidate => candidate.TenantId == context.TenantId && candidate.NormalizedKey == version.NormalizedKey),
            version,
            context,
            "ontology.versions.publish",
            request.Summary,
            cancellationToken);
        return ToOntologyVersionResponse(version);
    }

    public async Task<IReadOnlyCollection<SemanticLayerVersionResponse>> ListSemanticLayerVersionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.semantic_layers.list", OntologyPermissions.Read, cancellationToken);
        var versions = await SemanticLayerQuery(context.TenantId)
            .OrderBy(version => version.Key)
            .ThenByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        return versions.Select(ToSemanticLayerVersionResponse).ToList();
    }

    public async Task<SemanticLayerVersionResponse> CreateSemanticLayerVersionAsync(CreateSemanticLayerVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateSemanticLayerValidator, request, cancellationToken);
        EnsureValidJsonOrNull(request.GraphNodeTypeMappingsJson, "Graph node type mappings must be valid JSON.");
        EnsureValidJsonOrNull(request.GraphRelationshipTypeMappingsJson, "Graph relationship type mappings must be valid JSON.");
        var context = await RequireOntologyPermissionAsync("ontology.semantic_layers.create", OntologyPermissions.Manage, cancellationToken);
        var ontology = await RequireOntologyVersionAsync(request.OntologyVersionId, context, "ontology.semantic_layers.create", cancellationToken);
        var normalizedKey = NormalizeKey(request.Key);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        await EnsureVersionLabelAvailableAsync(dbContext.SemanticLayerVersions, context.TenantId, normalizedKey, normalizedVersionLabel, "Semantic layer version label already exists for this key.", cancellationToken);

        var version = new SemanticLayerVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Key = NormalizeText(request.Key),
            NormalizedKey = normalizedKey,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            OntologyVersionId = ontology.Id,
            GraphNodeTypeMappingsJson = TrimOptional(request.GraphNodeTypeMappingsJson),
            GraphRelationshipTypeMappingsJson = TrimOptional(request.GraphRelationshipTypeMappingsJson),
            State = OntologyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.SemanticLayerVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "ontology.semantic_layers.create", $"Semantic layer version '{version.Key}' '{version.VersionLabel}' was created.", nameof(SemanticLayerVersion), version.Id, cancellationToken);
        version.OntologyVersion = ontology;
        return ToSemanticLayerVersionResponse(version);
    }

    public async Task<SemanticLayerVersionResponse> PublishSemanticLayerVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.semantic_layers.publish", OntologyPermissions.Publish, cancellationToken);
        var version = await RequireSemanticLayerVersionAsync(versionId, context, "ontology.semantic_layers.publish", cancellationToken);
        if (version.OntologyVersion?.State != OntologyPublicationState.Published)
        {
            throw new RequestValidationException("Semantic layer versions must reference a published ontology version before publishing.");
        }

        await PublishVersionGroupAsync(
            dbContext.SemanticLayerVersions.Where(candidate => candidate.TenantId == context.TenantId && candidate.NormalizedKey == version.NormalizedKey),
            version,
            context,
            "ontology.semantic_layers.publish",
            request.Summary,
            cancellationToken);
        return ToSemanticLayerVersionResponse(version);
    }

    public async Task<IReadOnlyCollection<LifecycleVocabularyVersionResponse>> ListLifecycleVocabularyVersionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.lifecycle_vocabularies.list", OntologyPermissions.Read, cancellationToken);
        var versions = await dbContext.LifecycleVocabularyVersions
            .AsNoTracking()
            .Include(version => version.States)
            .Include(version => version.Transitions)
            .Where(version => version.TenantId == context.TenantId)
            .OrderBy(version => version.Key)
            .ThenByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        return versions.Select(ToLifecycleVocabularyVersionResponse).ToList();
    }

    public async Task<LifecycleVocabularyVersionDetailResponse> GetLifecycleVocabularyVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.lifecycle_vocabularies.get", OntologyPermissions.Read, cancellationToken);
        var version = await RequireLifecycleVocabularyVersionAsync(versionId, context, "ontology.lifecycle_vocabularies.get", cancellationToken);
        return ToLifecycleVocabularyVersionDetailResponse(version);
    }

    public async Task<LifecycleVocabularyVersionResponse> CreateLifecycleVocabularyVersionAsync(CreateLifecycleVocabularyVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateLifecycleValidator, request, cancellationToken);
        ValidateLifecycleDefinition(request);
        var context = await RequireOntologyPermissionAsync("ontology.lifecycle_vocabularies.create", OntologyPermissions.Manage, cancellationToken);
        var normalizedKey = NormalizeKey(request.Key);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        await EnsureVersionLabelAvailableAsync(dbContext.LifecycleVocabularyVersions, context.TenantId, normalizedKey, normalizedVersionLabel, "Lifecycle vocabulary version label already exists for this key.", cancellationToken);

        var version = new LifecycleVocabularyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Key = NormalizeText(request.Key),
            NormalizedKey = normalizedKey,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            State = OntologyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            States = request.States.Select(item => new LifecycleStateDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                Key = NormalizeText(item.Key),
                NormalizedKey = NormalizeKey(item.Key),
                DisplayName = NormalizeText(item.DisplayName),
                NormalizedCategory = item.Category is null ? null : NormalizeKey(item.Category),
                SortOrder = item.SortOrder,
                IsTerminal = item.IsTerminal,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList(),
            Transitions = request.Transitions.Select(item => new LifecycleTransitionDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                FromStateKey = NormalizeText(item.FromStateKey),
                NormalizedFromStateKey = NormalizeKey(item.FromStateKey),
                ToStateKey = NormalizeText(item.ToStateKey),
                NormalizedToStateKey = NormalizeKey(item.ToStateKey),
                RequiresApproval = item.RequiresApproval,
                SafeSummary = TrimOptional(item.SafeSummary),
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList()
        };

        dbContext.LifecycleVocabularyVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "ontology.lifecycle_vocabularies.create", $"Lifecycle vocabulary version '{version.Key}' '{version.VersionLabel}' was created.", nameof(LifecycleVocabularyVersion), version.Id, cancellationToken);
        return ToLifecycleVocabularyVersionResponse(version);
    }

    public async Task<LifecycleVocabularyVersionResponse> PublishLifecycleVocabularyVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.lifecycle_vocabularies.publish", OntologyPermissions.Publish, cancellationToken);
        var version = await RequireLifecycleVocabularyVersionAsync(versionId, context, "ontology.lifecycle_vocabularies.publish", cancellationToken);
        ValidateLifecycleVersionReady(version);
        await PublishVersionGroupAsync(
            dbContext.LifecycleVocabularyVersions.Where(candidate => candidate.TenantId == context.TenantId && candidate.NormalizedKey == version.NormalizedKey),
            version,
            context,
            "ontology.lifecycle_vocabularies.publish",
            request.Summary,
            cancellationToken);
        return ToLifecycleVocabularyVersionResponse(version);
    }

    public async Task<IReadOnlyCollection<AttributeSchemaVersionResponse>> ListAttributeSchemaVersionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.attribute_schemas.list", OntologyPermissions.Read, cancellationToken);
        var versions = await AttributeSchemaQuery(context.TenantId)
            .OrderBy(version => version.Key)
            .ThenByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        return versions.Select(ToAttributeSchemaVersionResponse).ToList();
    }

    public async Task<AttributeSchemaVersionDetailResponse> GetAttributeSchemaVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.attribute_schemas.get", OntologyPermissions.Read, cancellationToken);
        var version = await RequireAttributeSchemaVersionAsync(versionId, context, "ontology.attribute_schemas.get", cancellationToken);
        return ToAttributeSchemaVersionDetailResponse(version);
    }

    public async Task<AttributeSchemaVersionResponse> CreateAttributeSchemaVersionAsync(CreateAttributeSchemaVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateAttributeSchemaValidator, request, cancellationToken);
        var context = await RequireOntologyPermissionAsync("ontology.attribute_schemas.create", OntologyPermissions.Manage, cancellationToken);
        var ontology = await RequireOntologyVersionAsync(request.OntologyVersionId, context, "ontology.attribute_schemas.create", cancellationToken);
        ValidateAttributeSchemaDefinition(request, ontology);
        var normalizedKey = NormalizeKey(request.Key);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        await EnsureVersionLabelAvailableAsync(dbContext.AttributeSchemaVersions, context.TenantId, normalizedKey, normalizedVersionLabel, "Attribute schema version label already exists for this key.", cancellationToken);

        var version = new AttributeSchemaVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Key = NormalizeText(request.Key),
            NormalizedKey = normalizedKey,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            OntologyVersionId = ontology.Id,
            State = OntologyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            Attributes = request.Attributes.Select(item => new AttributeDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                AttributeKey = NormalizeText(item.AttributeKey),
                NormalizedAttributeKey = NormalizeKey(item.AttributeKey),
                AppliesToObjectType = NormalizeText(item.AppliesToObjectType),
                NormalizedAppliesToObjectType = NormalizeKey(item.AppliesToObjectType),
                ValueType = item.ValueType,
                IsRequired = item.IsRequired,
                ValidationRulesJson = TrimOptional(item.ValidationRulesJson),
                Visibility = item.Visibility,
                RequiredPermissionKey = TrimOptional(item.RequiredPermissionKey),
                IsSearchable = item.IsSearchable,
                IsAiFacing = item.IsAiFacing,
                ClassificationKey = TrimOptional(item.ClassificationKey),
                DisplayName = TrimOptional(item.DisplayName),
                SafeSummary = NormalizeText(item.SafeSummary),
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList()
        };

        dbContext.AttributeSchemaVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "ontology.attribute_schemas.create", $"Attribute schema version '{version.Key}' '{version.VersionLabel}' was created.", nameof(AttributeSchemaVersion), version.Id, cancellationToken);
        version.OntologyVersion = ontology;
        return ToAttributeSchemaVersionResponse(version);
    }

    public async Task<AttributeSchemaVersionResponse> PublishAttributeSchemaVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.attribute_schemas.publish", OntologyPermissions.Publish, cancellationToken);
        var version = await RequireAttributeSchemaVersionAsync(versionId, context, "ontology.attribute_schemas.publish", cancellationToken);
        if (version.OntologyVersion?.State != OntologyPublicationState.Published)
        {
            throw new RequestValidationException("Attribute schema versions must reference a published ontology version before publishing.");
        }

        await PublishVersionGroupAsync(
            dbContext.AttributeSchemaVersions.Where(candidate => candidate.TenantId == context.TenantId && candidate.NormalizedKey == version.NormalizedKey),
            version,
            context,
            "ontology.attribute_schemas.publish",
            request.Summary,
            cancellationToken);
        return ToAttributeSchemaVersionResponse(version);
    }

    public async Task<IReadOnlyCollection<ModelPackageVersionResponse>> ListModelPackageVersionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.model_packages.list", OntologyPermissions.Read, cancellationToken);
        var versions = await ModelPackageQuery(context.TenantId)
            .OrderBy(version => version.Key)
            .ThenByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        return versions.Select(ToModelPackageVersionResponse).ToList();
    }

    public async Task<ModelPackageVersionResponse> GetModelPackageVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.model_packages.get", OntologyPermissions.Read, cancellationToken);
        var version = await RequireModelPackageVersionAsync(versionId, context, "ontology.model_packages.get", cancellationToken);
        return ToModelPackageVersionResponse(version);
    }

    public async Task<ModelPackagePreviewResponse> PreviewModelPackageAsync(ModelPackagePreviewRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.model_packages.preview", OntologyPermissions.Read, cancellationToken);
        return await BuildModelPackagePreviewAsync(context, request, requirePublished: false, cancellationToken);
    }

    public async Task<ModelPackageVersionResponse> CreateModelPackageVersionAsync(CreateModelPackageVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateModelPackageValidator, request, cancellationToken);
        var context = await RequireOntologyPermissionAsync("ontology.model_packages.create", OntologyPermissions.Manage, cancellationToken);
        var normalizedKey = NormalizeKey(request.Key);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        await EnsureVersionLabelAvailableAsync(dbContext.ModelPackageVersions, context.TenantId, normalizedKey, normalizedVersionLabel, "Model package version label already exists for this key.", cancellationToken);
        var preview = await BuildModelPackagePreviewAsync(
            context,
            new ModelPackagePreviewRequest(request.OntologyVersionId, request.SemanticLayerVersionId, request.LifecycleVocabularyVersionId, request.AttributeSchemaVersionId),
            requirePublished: false,
            cancellationToken);
        if (!preview.IsValid)
        {
            throw new RequestValidationException(string.Join("; ", preview.BlockingReasons));
        }

        var version = new ModelPackageVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Key = NormalizeText(request.Key),
            NormalizedKey = normalizedKey,
            Name = NormalizeText(request.Name),
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            OntologyVersionId = request.OntologyVersionId,
            SemanticLayerVersionId = request.SemanticLayerVersionId,
            LifecycleVocabularyVersionId = request.LifecycleVocabularyVersionId,
            AttributeSchemaVersionId = request.AttributeSchemaVersionId,
            State = OntologyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ModelPackageVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "ontology.model_packages.create", $"Model package version '{version.Key}' '{version.VersionLabel}' was created.", nameof(ModelPackageVersion), version.Id, cancellationToken);
        return ToModelPackageVersionResponse(await RequireModelPackageVersionAsync(version.Id, context, "ontology.model_packages.create", cancellationToken));
    }

    public async Task<ModelPackageVersionResponse> PublishModelPackageVersionAsync(Guid versionId, PublishOntologyVersionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.model_packages.publish", OntologyPermissions.Publish, cancellationToken);
        var version = await RequireModelPackageVersionAsync(versionId, context, "ontology.model_packages.publish", cancellationToken);
        var preview = await BuildModelPackagePreviewAsync(
            context,
            new ModelPackagePreviewRequest(version.OntologyVersionId, version.SemanticLayerVersionId, version.LifecycleVocabularyVersionId, version.AttributeSchemaVersionId),
            requirePublished: true,
            cancellationToken);
        if (!preview.IsValid)
        {
            throw new RequestValidationException(string.Join("; ", preview.BlockingReasons));
        }

        await PublishVersionGroupAsync(
            dbContext.ModelPackageVersions.Where(candidate => candidate.TenantId == context.TenantId && candidate.NormalizedKey == version.NormalizedKey),
            version,
            context,
            "ontology.model_packages.publish",
            request.Summary,
            cancellationToken);
        return ToModelPackageVersionResponse(version);
    }

    public async Task<ModelPackageVersionResponse?> GetActiveModelPackageAsync(string? key, CancellationToken cancellationToken)
    {
        var context = await RequireOntologyPermissionAsync("ontology.model_packages.active", OntologyPermissions.Read, cancellationToken);
        var query = ModelPackageQuery(context.TenantId)
            .Where(version => version.State == OntologyPublicationState.Published);
        if (!string.IsNullOrWhiteSpace(key))
        {
            var normalizedKey = NormalizeKey(key);
            query = query.Where(version => version.NormalizedKey == normalizedKey);
        }

        var version = await query
            .OrderByDescending(candidate => candidate.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return version is null ? null : ToModelPackageVersionResponse(version);
    }

    private async Task<ActiveTenantContext> RequireOntologyPermissionAsync(string action, string permissionKey, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, OntologyPermissions.Admin, cancellationToken);
        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks ontology permission.");
        }

        return context;
    }

    private async Task<OntologyVersion> RequireOntologyVersionAsync(Guid versionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var version = await dbContext.OntologyVersions
            .Include(item => item.ObjectTypes)
            .Include(item => item.RelationshipTypes)
            .Include(item => item.BomRelationships)
            .SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Ontology version was not found.");
        await EnsureSameTenantAsync(version.TenantId, context, action, "ontology_tenant_mismatch", "The requested ontology version belongs to a different tenant.", cancellationToken);
        return version;
    }

    private async Task<SemanticLayerVersion> RequireSemanticLayerVersionAsync(Guid versionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var version = await dbContext.SemanticLayerVersions
            .Include(item => item.OntologyVersion)
            .SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Semantic layer version was not found.");
        await EnsureSameTenantAsync(version.TenantId, context, action, "ontology_tenant_mismatch", "The requested semantic layer version belongs to a different tenant.", cancellationToken);
        return version;
    }

    private async Task<LifecycleVocabularyVersion> RequireLifecycleVocabularyVersionAsync(Guid versionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var version = await dbContext.LifecycleVocabularyVersions
            .Include(item => item.States)
            .Include(item => item.Transitions)
            .SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Lifecycle vocabulary version was not found.");
        await EnsureSameTenantAsync(version.TenantId, context, action, "ontology_tenant_mismatch", "The requested lifecycle vocabulary version belongs to a different tenant.", cancellationToken);
        return version;
    }

    private async Task<AttributeSchemaVersion> RequireAttributeSchemaVersionAsync(Guid versionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var version = await dbContext.AttributeSchemaVersions
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.ObjectTypes)
            .Include(item => item.Attributes)
            .SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Attribute schema version was not found.");
        await EnsureSameTenantAsync(version.TenantId, context, action, "ontology_tenant_mismatch", "The requested attribute schema version belongs to a different tenant.", cancellationToken);
        return version;
    }

    private async Task<ModelPackageVersion> RequireModelPackageVersionAsync(Guid versionId, ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        var version = await ModelPackageQuery(context.TenantId)
            .SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Model package version was not found.");
        await EnsureSameTenantAsync(version.TenantId, context, action, "ontology_tenant_mismatch", "The requested model package version belongs to a different tenant.", cancellationToken);
        return version;
    }

    private async Task EnsureSameTenantAsync(Guid resourceTenantId, ActiveTenantContext context, string action, string reason, string safeSummary, CancellationToken cancellationToken)
    {
        if (resourceTenantId == context.TenantId)
        {
            return;
        }

        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, reason, safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("Ontology resource is not available in the active tenant.");
    }

    private async Task<ModelPackagePreviewResponse> BuildModelPackagePreviewAsync(
        ActiveTenantContext context,
        ModelPackagePreviewRequest request,
        bool requirePublished,
        CancellationToken cancellationToken)
    {
        var blockingReasons = new List<string>();
        var ontology = await dbContext.OntologyVersions.AsNoTracking().SingleOrDefaultAsync(version => version.Id == request.OntologyVersionId, cancellationToken);
        var semanticLayer = await dbContext.SemanticLayerVersions.AsNoTracking().SingleOrDefaultAsync(version => version.Id == request.SemanticLayerVersionId, cancellationToken);
        var lifecycle = await dbContext.LifecycleVocabularyVersions.AsNoTracking().SingleOrDefaultAsync(version => version.Id == request.LifecycleVocabularyVersionId, cancellationToken);
        var attributeSchema = await dbContext.AttributeSchemaVersions.AsNoTracking().SingleOrDefaultAsync(version => version.Id == request.AttributeSchemaVersionId, cancellationToken);

        ValidateReferencedVersion(ontology, context.TenantId, requirePublished, "Ontology", blockingReasons);
        ValidateReferencedVersion(semanticLayer, context.TenantId, requirePublished, "Semantic layer", blockingReasons);
        ValidateReferencedVersion(lifecycle, context.TenantId, requirePublished, "Lifecycle vocabulary", blockingReasons);
        ValidateReferencedVersion(attributeSchema, context.TenantId, requirePublished, "Attribute schema", blockingReasons);

        if (semanticLayer is not null && ontology is not null && semanticLayer.OntologyVersionId != ontology.Id)
        {
            blockingReasons.Add("Semantic layer must reference the same ontology version as the model package.");
        }

        if (attributeSchema is not null && ontology is not null && attributeSchema.OntologyVersionId != ontology.Id)
        {
            blockingReasons.Add("Attribute schema must reference the same ontology version as the model package.");
        }

        return new ModelPackagePreviewResponse(
            blockingReasons.Count == 0,
            blockingReasons,
            request.OntologyVersionId,
            request.SemanticLayerVersionId,
            request.LifecycleVocabularyVersionId,
            request.AttributeSchemaVersionId);
    }

    private static void ValidateReferencedVersion<TVersion>(
        TVersion? version,
        Guid tenantId,
        bool requirePublished,
        string label,
        ICollection<string> blockingReasons)
        where TVersion : class, ITenantVersion
    {
        if (version is null)
        {
            blockingReasons.Add($"{label} version was not found.");
            return;
        }

        if (version.TenantId != tenantId)
        {
            blockingReasons.Add($"{label} version belongs to a different tenant.");
        }

        if (requirePublished && version.State != OntologyPublicationState.Published)
        {
            blockingReasons.Add($"{label} version must be published.");
        }
    }

    private async Task PublishVersionGroupAsync<TVersion>(
        IQueryable<TVersion> versionGroup,
        TVersion version,
        ActiveTenantContext context,
        string action,
        string? summary,
        CancellationToken cancellationToken)
        where TVersion : class, IMutablePublishedVersion
    {
        if (version.State == OntologyPublicationState.Published)
        {
            return;
        }

        var activeVersions = await versionGroup
            .Where(candidate => EF.Property<OntologyPublicationState>(candidate, nameof(ITenantVersion.State)) == OntologyPublicationState.Published)
            .ToListAsync(cancellationToken);
        foreach (var activeVersion in activeVersions)
        {
            activeVersion.State = OntologyPublicationState.Retired;
        }

        version.State = OntologyPublicationState.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.PublishedByUserId = context.UserId;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            version.Summary = summary.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, action, $"Ontology resource version '{version.VersionLabel}' was published.", version.GetType().Name, version.Id, cancellationToken);
    }

    private IQueryable<SemanticLayerVersion> SemanticLayerQuery(Guid tenantId)
    {
        return dbContext.SemanticLayerVersions
            .AsNoTracking()
            .Include(version => version.OntologyVersion)
            .Where(version => version.TenantId == tenantId);
    }

    private IQueryable<AttributeSchemaVersion> AttributeSchemaQuery(Guid tenantId)
    {
        return dbContext.AttributeSchemaVersions
            .AsNoTracking()
            .Include(version => version.OntologyVersion)
            .Include(version => version.Attributes)
            .Where(version => version.TenantId == tenantId);
    }

    private IQueryable<ModelPackageVersion> ModelPackageQuery(Guid tenantId)
    {
        return dbContext.ModelPackageVersions
            .Include(version => version.OntologyVersion)
            .Include(version => version.SemanticLayerVersion)
            .Include(version => version.LifecycleVocabularyVersion)
            .Include(version => version.AttributeSchemaVersion)
            .Where(version => version.TenantId == tenantId);
    }

    private async Task RecordAuditAsync(ActiveTenantContext context, string action, string safeSummary, string sourceObjectType, Guid sourceObjectId, CancellationToken cancellationToken)
    {
        await auditRecorder.RecordAsync(
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

    private static void ValidateOntologyDefinition(CreateOntologyVersionRequest request)
    {
        var objectTypes = request.ObjectTypes.Select(item => NormalizeKey(item.Key)).ToHashSet();
        if (objectTypes.Count != request.ObjectTypes.Count)
        {
            throw new RequestValidationException("Object type keys must be unique within an ontology version.");
        }

        foreach (var item in request.ObjectTypes)
        {
            EnsureValidJsonOrNull(item.VersionIdentityFieldsJson, "Version identity fields must be valid JSON.");
        }

        foreach (var relationship in request.RelationshipTypes)
        {
            if (!objectTypes.Contains(NormalizeKey(relationship.FromObjectType)) || !objectTypes.Contains(NormalizeKey(relationship.ToObjectType)))
            {
                throw new RequestValidationException("Semantic relationships must reference defined object types.");
            }
        }

        foreach (var bom in request.BomRelationships)
        {
            if (!objectTypes.Contains(NormalizeKey(bom.ParentObjectType)) || !objectTypes.Contains(NormalizeKey(bom.ChildObjectType)))
            {
                throw new RequestValidationException("BOM relationships must reference defined object types.");
            }

            EnsureValidJsonOrNull(bom.LifecycleConstraintJson, "BOM lifecycle constraints must be valid JSON.");
        }
    }

    private static void ValidateOntologyVersionReady(OntologyVersion version)
    {
        if (version.ObjectTypes.Count == 0)
        {
            throw new RequestValidationException("Ontology versions must define at least one object type before publishing.");
        }
    }

    private static void ValidateLifecycleDefinition(CreateLifecycleVocabularyVersionRequest request)
    {
        var states = request.States.Select(item => NormalizeKey(item.Key)).ToHashSet();
        if (states.Count != request.States.Count)
        {
            throw new RequestValidationException("Lifecycle state keys must be unique within a vocabulary version.");
        }

        foreach (var transition in request.Transitions)
        {
            if (!states.Contains(NormalizeKey(transition.FromStateKey)) || !states.Contains(NormalizeKey(transition.ToStateKey)))
            {
                throw new RequestValidationException("Lifecycle transitions must reference defined states.");
            }
        }
    }

    private static void ValidateLifecycleVersionReady(LifecycleVocabularyVersion version)
    {
        if (version.States.Count == 0)
        {
            throw new RequestValidationException("Lifecycle vocabularies must define at least one state before publishing.");
        }
    }

    private static void ValidateAttributeSchemaDefinition(CreateAttributeSchemaVersionRequest request, OntologyVersion ontology)
    {
        var objectTypes = ontology.ObjectTypes.Select(item => item.NormalizedKey).ToHashSet();
        var attributeKeys = new HashSet<string>();
        foreach (var attribute in request.Attributes)
        {
            if (!objectTypes.Contains(NormalizeKey(attribute.AppliesToObjectType)))
            {
                throw new RequestValidationException("Attributes must apply to object types defined by the referenced ontology version.");
            }

            var scopedKey = $"{NormalizeKey(attribute.AppliesToObjectType)}:{NormalizeKey(attribute.AttributeKey)}";
            if (!attributeKeys.Add(scopedKey))
            {
                throw new RequestValidationException("Attribute keys must be unique per object type within a schema version.");
            }

            if (attribute.Visibility == AttributeVisibility.Restricted && string.IsNullOrWhiteSpace(attribute.RequiredPermissionKey))
            {
                throw new RequestValidationException("Restricted attributes must define a required permission key.");
            }

            EnsureValidJsonOrNull(attribute.ValidationRulesJson, "Attribute validation rules must be valid JSON.");
        }
    }

    private static async Task EnsureVersionLabelAvailableAsync<TVersion>(
        IQueryable<TVersion> versions,
        Guid tenantId,
        string normalizedKey,
        string normalizedVersionLabel,
        string message,
        CancellationToken cancellationToken)
        where TVersion : class, ITenantVersion
    {
        var exists = await versions.AnyAsync(
            version => EF.Property<Guid>(version, nameof(ITenantScoped.TenantId)) == tenantId
                && EF.Property<string>(version, nameof(ITenantVersion.NormalizedKey)) == normalizedKey
                && EF.Property<string>(version, nameof(ITenantVersion.NormalizedVersionLabel)) == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException(message);
        }
    }

    private static OntologyVersionResponse ToOntologyVersionResponse(OntologyVersion version)
    {
        return new OntologyVersionResponse(
            version.Id,
            version.TenantId,
            version.Key,
            version.VersionLabel,
            version.Summary,
            version.State,
            version.ObjectTypes.Count,
            version.RelationshipTypes.Count,
            version.BomRelationships.Count,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);
    }

    private static OntologyVersionDetailResponse ToOntologyVersionDetailResponse(OntologyVersion version)
    {
        return new OntologyVersionDetailResponse(
            ToOntologyVersionResponse(version),
            version.ObjectTypes.OrderBy(item => item.Key).Select(item => new ObjectTypeDefinitionResponse(item.Id, item.Key, item.DisplayName, item.Description, item.VersionIdentityFieldsJson, item.SafeSummary)).ToList(),
            version.RelationshipTypes.OrderBy(item => item.RelationshipType).Select(item => new SemanticRelationshipDefinitionResponse(item.Id, item.RelationshipType, item.FromObjectType, item.ToObjectType, item.Description, item.IsVersionRelationship)).ToList(),
            version.BomRelationships.OrderBy(item => item.RelationshipType).Select(item => new BomRelationshipDefinitionResponse(item.Id, item.RelationshipType, item.ParentObjectType, item.ChildObjectType, item.QuantityAttributeKey, item.UnitAttributeKey, item.FindNumberAttributeKey, item.ReferenceDesignatorAttributeKey, item.LifecycleConstraintJson, item.RequiresApproval, item.AuditReferenceAttributeKey)).ToList());
    }

    private static SemanticLayerVersionResponse ToSemanticLayerVersionResponse(SemanticLayerVersion version)
    {
        return new SemanticLayerVersionResponse(
            version.Id,
            version.TenantId,
            version.Key,
            version.VersionLabel,
            version.Summary,
            version.OntologyVersionId,
            version.OntologyVersion?.VersionLabel,
            version.GraphNodeTypeMappingsJson,
            version.GraphRelationshipTypeMappingsJson,
            version.State,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);
    }

    private static LifecycleVocabularyVersionResponse ToLifecycleVocabularyVersionResponse(LifecycleVocabularyVersion version)
    {
        return new LifecycleVocabularyVersionResponse(
            version.Id,
            version.TenantId,
            version.Key,
            version.VersionLabel,
            version.Summary,
            version.State,
            version.States.Count,
            version.Transitions.Count,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);
    }

    private static LifecycleVocabularyVersionDetailResponse ToLifecycleVocabularyVersionDetailResponse(LifecycleVocabularyVersion version)
    {
        return new LifecycleVocabularyVersionDetailResponse(
            ToLifecycleVocabularyVersionResponse(version),
            version.States.OrderBy(item => item.SortOrder).Select(item => new LifecycleStateDefinitionResponse(item.Id, item.Key, item.DisplayName, item.NormalizedCategory, item.SortOrder, item.IsTerminal)).ToList(),
            version.Transitions.OrderBy(item => item.FromStateKey).ThenBy(item => item.ToStateKey).Select(item => new LifecycleTransitionDefinitionResponse(item.Id, item.FromStateKey, item.ToStateKey, item.RequiresApproval, item.SafeSummary)).ToList());
    }

    private static AttributeSchemaVersionResponse ToAttributeSchemaVersionResponse(AttributeSchemaVersion version)
    {
        return new AttributeSchemaVersionResponse(
            version.Id,
            version.TenantId,
            version.Key,
            version.VersionLabel,
            version.Summary,
            version.OntologyVersionId,
            version.OntologyVersion?.VersionLabel,
            version.State,
            version.Attributes.Count,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);
    }

    private static AttributeSchemaVersionDetailResponse ToAttributeSchemaVersionDetailResponse(AttributeSchemaVersion version)
    {
        return new AttributeSchemaVersionDetailResponse(
            ToAttributeSchemaVersionResponse(version),
            version.Attributes.OrderBy(item => item.AppliesToObjectType).ThenBy(item => item.AttributeKey).Select(item => new AttributeDefinitionResponse(item.Id, item.AttributeKey, item.AppliesToObjectType, item.ValueType, item.IsRequired, item.ValidationRulesJson, item.Visibility, item.RequiredPermissionKey, item.IsSearchable, item.IsAiFacing, item.ClassificationKey, item.DisplayName, item.SafeSummary)).ToList());
    }

    private static ModelPackageVersionResponse ToModelPackageVersionResponse(ModelPackageVersion version)
    {
        return new ModelPackageVersionResponse(
            version.Id,
            version.TenantId,
            version.Key,
            version.Name,
            version.VersionLabel,
            version.Summary,
            version.OntologyVersionId,
            version.OntologyVersion?.VersionLabel,
            version.SemanticLayerVersionId,
            version.SemanticLayerVersion?.VersionLabel,
            version.LifecycleVocabularyVersionId,
            version.LifecycleVocabularyVersion?.VersionLabel,
            version.AttributeSchemaVersionId,
            version.AttributeSchemaVersion?.VersionLabel,
            version.ArtifactId,
            version.ArtifactVersionId,
            version.State,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
        }
    }

    private static void EnsureValidJsonOrNull(string? json, string message)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new RequestValidationException(message);
        }
    }

    private static string NormalizeText(string value) => value.Trim();
    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CreateOntologyVersionRequestValidator : AbstractValidator<CreateOntologyVersionRequest>
    {
        public CreateOntologyVersionRequestValidator()
        {
            RuleFor(request => request.Key).NotEmpty().MaximumLength(120);
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.ObjectTypes).NotEmpty();
            RuleForEach(request => request.ObjectTypes).ChildRules(item =>
            {
                item.RuleFor(value => value.Key).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.DisplayName).NotEmpty().MaximumLength(200);
                item.RuleFor(value => value.Description).MaximumLength(1000);
                item.RuleFor(value => value.VersionIdentityFieldsJson).MaximumLength(4000);
                item.RuleFor(value => value.SafeSummary).NotEmpty().MaximumLength(1000);
            });
            RuleForEach(request => request.RelationshipTypes).ChildRules(item =>
            {
                item.RuleFor(value => value.RelationshipType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.FromObjectType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.ToObjectType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.Description).MaximumLength(1000);
            });
            RuleForEach(request => request.BomRelationships).ChildRules(item =>
            {
                item.RuleFor(value => value.RelationshipType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.ParentObjectType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.ChildObjectType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.LifecycleConstraintJson).MaximumLength(4000);
            });
        }
    }

    private sealed class CreateSemanticLayerVersionRequestValidator : AbstractValidator<CreateSemanticLayerVersionRequest>
    {
        public CreateSemanticLayerVersionRequestValidator()
        {
            RuleFor(request => request.Key).NotEmpty().MaximumLength(120);
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.OntologyVersionId).NotEmpty();
            RuleFor(request => request.GraphNodeTypeMappingsJson).MaximumLength(8000);
            RuleFor(request => request.GraphRelationshipTypeMappingsJson).MaximumLength(8000);
        }
    }

    private sealed class CreateLifecycleVocabularyVersionRequestValidator : AbstractValidator<CreateLifecycleVocabularyVersionRequest>
    {
        public CreateLifecycleVocabularyVersionRequestValidator()
        {
            RuleFor(request => request.Key).NotEmpty().MaximumLength(120);
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.States).NotEmpty();
            RuleForEach(request => request.States).ChildRules(item =>
            {
                item.RuleFor(value => value.Key).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.DisplayName).NotEmpty().MaximumLength(200);
                item.RuleFor(value => value.Category).MaximumLength(120);
            });
            RuleForEach(request => request.Transitions).ChildRules(item =>
            {
                item.RuleFor(value => value.FromStateKey).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.ToStateKey).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.SafeSummary).MaximumLength(1000);
            });
        }
    }

    private sealed class CreateAttributeSchemaVersionRequestValidator : AbstractValidator<CreateAttributeSchemaVersionRequest>
    {
        public CreateAttributeSchemaVersionRequestValidator()
        {
            RuleFor(request => request.Key).NotEmpty().MaximumLength(120);
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.OntologyVersionId).NotEmpty();
            RuleFor(request => request.Attributes).NotEmpty();
            RuleForEach(request => request.Attributes).ChildRules(item =>
            {
                item.RuleFor(value => value.AttributeKey).NotEmpty().MaximumLength(160);
                item.RuleFor(value => value.AppliesToObjectType).NotEmpty().MaximumLength(120);
                item.RuleFor(value => value.ValidationRulesJson).MaximumLength(4000);
                item.RuleFor(value => value.RequiredPermissionKey).MaximumLength(160);
                item.RuleFor(value => value.ClassificationKey).MaximumLength(120);
                item.RuleFor(value => value.DisplayName).MaximumLength(200);
                item.RuleFor(value => value.SafeSummary).NotEmpty().MaximumLength(1000);
            });
        }
    }

    private sealed class CreateModelPackageVersionRequestValidator : AbstractValidator<CreateModelPackageVersionRequest>
    {
        public CreateModelPackageVersionRequestValidator()
        {
            RuleFor(request => request.Key).NotEmpty().MaximumLength(120);
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.OntologyVersionId).NotEmpty();
            RuleFor(request => request.SemanticLayerVersionId).NotEmpty();
            RuleFor(request => request.LifecycleVocabularyVersionId).NotEmpty();
            RuleFor(request => request.AttributeSchemaVersionId).NotEmpty();
        }
    }
}
