using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using ETOS.Backend.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Ontology;

public interface IModelPackageContextResolver
{
    Task<ResolvedModelPackageContext> ResolvePublishedAsync(
        Guid modelPackageVersionId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken);

    Task<ResolvedModelPackageContext?> ResolveActivePublishedAsync(
        ActiveTenantContext context,
        string? packageKey,
        CancellationToken cancellationToken);
}

public sealed record ResolvedModelPackageContext(
    ModelPackageVersion ModelPackage,
    OntologyVersion Ontology,
    SemanticLayerVersion SemanticLayer,
    LifecycleVocabularyVersion LifecycleVocabulary,
    AttributeSchemaVersion AttributeSchema,
    ModelPackageImportProfile ImportProfile,
    ModelPackageQueryIntentExtensions QueryIntentExtensions,
    IReadOnlyDictionary<string, string> GraphNodeTypeMappings,
    IReadOnlyDictionary<string, string> GraphRelationshipTypeMappings,
    BomRelationshipDefinition? DefaultBomRelationship)
{
    public string ResolveGraphObjectType(string ontologyObjectType)
    {
        return GraphNodeTypeMappings.TryGetValue(ontologyObjectType, out var mapped)
            ? mapped
            : ontologyObjectType;
    }

    public string ResolveGraphRelationshipType(string ontologyRelationshipType)
    {
        return GraphRelationshipTypeMappings.TryGetValue(ontologyRelationshipType, out var mapped)
            ? mapped
            : ontologyRelationshipType;
    }

    public BomRelationshipDefinition RequireDefaultBomRelationship()
    {
        return DefaultBomRelationship
            ?? throw new RequestValidationException("Active model package does not define a BOM relationship for structural import.");
    }
}

public sealed class ModelPackageContextResolver(
    EnterpriseThreadDbContext dbContext,
    IAccessDenialRecorder denialRecorder) : IModelPackageContextResolver
{
    public async Task<ResolvedModelPackageContext> ResolvePublishedAsync(
        Guid modelPackageVersionId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var modelPackage = await dbContext.ModelPackageVersions
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.ObjectTypes)
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.RelationshipTypes)
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.BomRelationships)
            .Include(item => item.SemanticLayerVersion)
            .Include(item => item.LifecycleVocabularyVersion)
            .ThenInclude(item => item!.States)
            .Include(item => item.AttributeSchemaVersion)
            .ThenInclude(item => item!.Attributes)
            .SingleOrDefaultAsync(item => item.Id == modelPackageVersionId, cancellationToken)
            ?? throw new RequestValidationException("Referenced model package version was not found.");

        await EnsureSameTenantAsync(modelPackage.TenantId, context, action, cancellationToken);
        EnsurePublished(modelPackage);
        return Build(modelPackage);
    }

    public async Task<ResolvedModelPackageContext?> ResolveActivePublishedAsync(
        ActiveTenantContext context,
        string? packageKey,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ModelPackageVersions
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.ObjectTypes)
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.RelationshipTypes)
            .Include(item => item.OntologyVersion)
            .ThenInclude(item => item!.BomRelationships)
            .Include(item => item.SemanticLayerVersion)
            .Include(item => item.LifecycleVocabularyVersion)
            .ThenInclude(item => item!.States)
            .Include(item => item.AttributeSchemaVersion)
            .ThenInclude(item => item!.Attributes)
            .Where(item => item.TenantId == context.TenantId && item.State == OntologyPublicationState.Published);

        if (!string.IsNullOrWhiteSpace(packageKey))
        {
            var normalizedKey = NormalizeKey(packageKey);
            query = query.Where(item => item.NormalizedKey == normalizedKey);
        }

        var modelPackage = await query
            .OrderByDescending(item => item.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return modelPackage is null ? null : Build(modelPackage);
    }

    private static ResolvedModelPackageContext Build(ModelPackageVersion modelPackage)
    {
        var ontology = modelPackage.OntologyVersion
            ?? throw new RequestValidationException("Model package ontology version was not found.");
        var semanticLayer = modelPackage.SemanticLayerVersion
            ?? throw new RequestValidationException("Model package semantic layer version was not found.");
        var lifecycle = modelPackage.LifecycleVocabularyVersion
            ?? throw new RequestValidationException("Model package lifecycle vocabulary version was not found.");
        var attributeSchema = modelPackage.AttributeSchemaVersion
            ?? throw new RequestValidationException("Model package attribute schema version was not found.");

        var importProfile = ModelPackageProfileParser.ParseImportProfile(modelPackage.ImportProfileJson);
        var queryIntentExtensions = ModelPackageProfileParser.ParseQueryIntentExtensions(modelPackage.QueryIntentExtensionsJson);
        var defaultBom = ResolveDefaultBomRelationship(ontology, importProfile);

        return new ResolvedModelPackageContext(
            modelPackage,
            ontology,
            semanticLayer,
            lifecycle,
            attributeSchema,
            importProfile,
            queryIntentExtensions,
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphNodeTypeMappingsJson),
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphRelationshipTypeMappingsJson),
            defaultBom);
    }

    private static BomRelationshipDefinition? ResolveDefaultBomRelationship(
        OntologyVersion ontology,
        ModelPackageImportProfile importProfile)
    {
        if (!string.IsNullOrWhiteSpace(importProfile.DefaultBomRelationshipType))
        {
            var normalized = NormalizeKey(importProfile.DefaultBomRelationshipType);
            return ontology.BomRelationships.FirstOrDefault(item => item.NormalizedRelationshipType == normalized)
                ?? ontology.BomRelationships.FirstOrDefault();
        }

        return ontology.BomRelationships.OrderBy(item => item.RelationshipType).FirstOrDefault();
    }

    private static void EnsurePublished(ModelPackageVersion modelPackage)
    {
        if (modelPackage.State != OntologyPublicationState.Published
            || modelPackage.OntologyVersion?.State != OntologyPublicationState.Published
            || modelPackage.SemanticLayerVersion?.State != OntologyPublicationState.Published
            || modelPackage.LifecycleVocabularyVersion?.State != OntologyPublicationState.Published
            || modelPackage.AttributeSchemaVersion?.State != OntologyPublicationState.Published)
        {
            throw new RequestValidationException("Import mappings require a published model package and published model package parts.");
        }
    }

    private async Task EnsureSameTenantAsync(
        Guid resourceTenantId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        if (resourceTenantId == context.TenantId)
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "model_package_tenant_mismatch",
            "The referenced model package belongs to a different tenant.",
            cancellationToken);
        throw new TenantAccessDeniedException("Model package is not available in the active tenant.");
    }

    private static string NormalizeKey(string value) =>
        value.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
}
