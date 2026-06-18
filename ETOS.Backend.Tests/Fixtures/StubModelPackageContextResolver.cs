using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;
using ETOS.Backend.Packages;

namespace ETOS.Backend.Tests.Fixtures;

public sealed class StubModelPackageContextResolver : IModelPackageContextResolver
{
    private static readonly Lazy<LoadedReferencePackageManifest> LoadedManifest = new(LoadManifest);

    public Task<ResolvedModelPackageContext> ResolvePublishedAsync(
        Guid modelPackageVersionId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
        => Task.FromResult(BuildDefault(context.TenantId, modelPackageVersionId));

    public Task<ResolvedModelPackageContext?> ResolveActivePublishedAsync(
        ActiveTenantContext context,
        string? packageKey,
        CancellationToken cancellationToken)
        => Task.FromResult<ResolvedModelPackageContext?>(BuildDefault(context.TenantId, Guid.NewGuid()));

    private static LoadedReferencePackageManifest LoadManifest()
    {
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));
        var environment = new ReferencePackageTestHostEnvironment(packagesRoot);
        var loader = new ReferencePackageManifestLoader(
            environment,
            Microsoft.Extensions.Options.Options.Create(new ReferencePackageOptions { RootPath = packagesRoot }));
        return loader.Load(ManufacturingReferencePackageKeys.PackageKey);
    }

    private static ResolvedModelPackageContext BuildDefault(Guid tenantId, Guid packageId)
    {
        var loaded = LoadedManifest.Value;
        var bomRelationship = loaded.BomRelationships[0];
        var ontology = new OntologyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = loaded.Manifest.Ontology.Key,
            NormalizedKey = loaded.Manifest.Ontology.Key.Replace('-', '_'),
            VersionLabel = loaded.Manifest.Ontology.VersionLabel,
            NormalizedVersionLabel = loaded.Manifest.Ontology.VersionLabel,
            ObjectTypes =
            [
                new OntologyObjectTypeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = "part",
                    NormalizedKey = "part",
                    DisplayName = "Part",
                    VersionIdentityFieldsJson = """["partNumber"]""",
                    SafeSummary = "Part identity."
                }
            ],
            BomRelationships =
            [
                new BomRelationshipDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RelationshipType = bomRelationship.RelationshipType,
                    NormalizedRelationshipType = bomRelationship.RelationshipType,
                    ParentObjectType = bomRelationship.ParentObjectType,
                    NormalizedParentObjectType = bomRelationship.ParentObjectType,
                    ChildObjectType = bomRelationship.ChildObjectType,
                    NormalizedChildObjectType = bomRelationship.ChildObjectType,
                    QuantityAttributeKey = bomRelationship.QuantityAttributeKey,
                    UnitAttributeKey = bomRelationship.UnitAttributeKey,
                    FindNumberAttributeKey = bomRelationship.FindNumberAttributeKey,
                    ReferenceDesignatorAttributeKey = bomRelationship.ReferenceDesignatorAttributeKey
                }
            ]
        };
        var semanticLayer = new SemanticLayerVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = loaded.Manifest.SemanticLayer.Key,
            NormalizedKey = loaded.Manifest.SemanticLayer.Key.Replace('-', '_'),
            VersionLabel = loaded.Manifest.SemanticLayer.VersionLabel,
            NormalizedVersionLabel = loaded.Manifest.SemanticLayer.VersionLabel,
            GraphNodeTypeMappingsJson = loaded.SemanticLayerMappings.GraphNodeTypeMappingsJson,
            GraphRelationshipTypeMappingsJson = loaded.SemanticLayerMappings.GraphRelationshipTypeMappingsJson
        };
        var lifecycle = new LifecycleVocabularyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = loaded.Manifest.Lifecycle.Key,
            NormalizedKey = loaded.Manifest.Lifecycle.Key.Replace('-', '_'),
            VersionLabel = loaded.Manifest.Lifecycle.VersionLabel,
            NormalizedVersionLabel = loaded.Manifest.Lifecycle.VersionLabel,
            States =
            [
                new LifecycleStateDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = "released",
                    NormalizedKey = "released",
                    DisplayName = "Released",
                    SortOrder = 1
                }
            ]
        };
        var attributeSchema = new AttributeSchemaVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = loaded.Manifest.AttributeSchema.Key,
            NormalizedKey = loaded.Manifest.AttributeSchema.Key.Replace('-', '_'),
            VersionLabel = loaded.Manifest.AttributeSchema.VersionLabel,
            NormalizedVersionLabel = loaded.Manifest.AttributeSchema.VersionLabel,
            Attributes =
            [
                new AttributeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AttributeKey = "partNumber",
                    NormalizedAttributeKey = "partnumber",
                    AppliesToObjectType = "part",
                    NormalizedAppliesToObjectType = "part",
                    IsRequired = true,
                    SafeSummary = "Part number"
                }
            ]
        };
        var modelPackage = new ModelPackageVersion
        {
            Id = packageId,
            TenantId = tenantId,
            Key = loaded.Manifest.PackageKey,
            NormalizedKey = loaded.Manifest.PackageKey.Replace('-', '_'),
            Name = loaded.Manifest.Name,
            VersionLabel = loaded.Manifest.VersionLabel,
            NormalizedVersionLabel = loaded.Manifest.VersionLabel,
            OntologyVersionId = ontology.Id,
            SemanticLayerVersionId = semanticLayer.Id,
            LifecycleVocabularyVersionId = lifecycle.Id,
            AttributeSchemaVersionId = attributeSchema.Id,
            ImportProfileJson = System.Text.Json.JsonSerializer.Serialize(loaded.ImportProfile),
            QueryIntentExtensionsJson = System.Text.Json.JsonSerializer.Serialize(loaded.QueryIntentExtensions),
            State = OntologyPublicationState.Published
        };

        return new ResolvedModelPackageContext(
            modelPackage,
            ontology,
            semanticLayer,
            lifecycle,
            attributeSchema,
            loaded.ImportProfile,
            loaded.QueryIntentExtensions,
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphNodeTypeMappingsJson),
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphRelationshipTypeMappingsJson),
            ontology.BomRelationships[0]);
    }
}
