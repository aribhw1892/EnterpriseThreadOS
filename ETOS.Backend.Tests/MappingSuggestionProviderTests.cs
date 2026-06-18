using ETOS.Backend.Imports;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Ontology;
using ETOS.Backend.Tests.Fixtures;

namespace ETOS.Backend.Tests;

public sealed class MappingSuggestionProviderTests
{
    [Fact]
    public async Task RuleBasedProviderMatchesOntologyAttributesAndLifecycle()
    {
        var ontology = new OntologyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Key = "demo",
            NormalizedKey = "demo",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            ObjectTypes =
            [
                new OntologyObjectTypeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = Guid.NewGuid(),
                    Key = "part",
                    NormalizedKey = "part",
                    DisplayName = "Part",
                    SafeSummary = "Part"
                }
            ]
        };
        var lifecycle = new LifecycleVocabularyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Key = "lifecycle",
            NormalizedKey = "lifecycle",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            States =
            [
                new LifecycleStateDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = Guid.NewGuid(),
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
            TenantId = Guid.NewGuid(),
            Key = "attributes",
            NormalizedKey = "attributes",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Attributes =
            [
                new AttributeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = Guid.NewGuid(),
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
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Key = "pkg",
            NormalizedKey = "pkg",
            Name = "Package",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            ImportProfileJson = ReferencePackageTestProfiles.ImportProfileJson,
            QueryIntentExtensionsJson = ReferencePackageTestProfiles.QueryIntentExtensionsJson
        };
        var semanticLayer = new SemanticLayerVersion
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Key = "semantic",
            NormalizedKey = "semantic",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            GraphNodeTypeMappingsJson = """{"part":"part"}""",
            GraphRelationshipTypeMappingsJson = """{"contains":"BOM_CONTAINS"}"""
        };
        var resolved = new ResolvedModelPackageContext(
            modelPackage,
            ontology,
            semanticLayer,
            lifecycle,
            attributeSchema,
            ModelPackageProfileParser.ParseImportProfile(modelPackage.ImportProfileJson),
            ModelPackageProfileParser.ParseQueryIntentExtensions(modelPackage.QueryIntentExtensionsJson),
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphNodeTypeMappingsJson),
            ModelPackageProfileParser.ParseStringDictionary(semanticLayer.GraphRelationshipTypeMappingsJson),
            ontology.BomRelationships.FirstOrDefault());

        var provider = new RuleBasedMappingProvider();
        var result = await provider.SuggestAsync(
            new ImportMappingSuggestionRequest(
                ["partNumber", "lifecycle"],
                [new Dictionary<string, string?> { ["partNumber"] = "P-1", ["lifecycle"] = "released" }],
                resolved),
            CancellationToken.None);

        Assert.Equal(MappingSuggestionProviderKeys.RuleBased, result.ProviderKey);
        var column = Assert.Single(result.ColumnSuggestions, item => item.SourceColumn == "partNumber");
        Assert.Equal("part", column.CanonicalObjectType);
        Assert.Equal("partNumber", column.CanonicalAttributeKey);
        var lifecycleSuggestion = Assert.Single(result.LifecycleSuggestions);
        Assert.Equal("released", lifecycleSuggestion.CanonicalLifecycleKey);
    }
}
