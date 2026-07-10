using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports;

internal sealed record ResolvedStructuralRelationship(
    string RelationshipType,
    string ParentObjectType,
    string ChildObjectType,
    BomRelationshipDefinition? BomRelationship,
    SemanticRelationshipDefinition? SemanticRelationship)
{
    public bool IsBomRelationship => BomRelationship is not null;
}

internal static class StructuralRelationshipResolver
{
    internal static ResolvedStructuralRelationship Resolve(
        ResolvedModelPackageContext modelContext,
        ImportMappingVersion mapping,
        ImportStructuralImportHelper.StructuralHeaders structuralHeaders)
    {
        var parentObjectType = ResolveEndpointObjectType(mapping, structuralHeaders.ParentHeader);
        var childObjectType = ResolveEndpointObjectType(mapping, structuralHeaders.ChildHeader);

        if (!string.IsNullOrWhiteSpace(mapping.StructuralRelationshipType))
        {
            return ResolveExplicit(
                modelContext,
                mapping.StructuralRelationshipType,
                parentObjectType,
                childObjectType);
        }

        var defaultBom = modelContext.RequireDefaultBomRelationship();
        ValidateEndpointTypes(defaultBom.ParentObjectType, defaultBom.ChildObjectType, parentObjectType, childObjectType, defaultBom.RelationshipType);
        return new ResolvedStructuralRelationship(
            defaultBom.RelationshipType,
            defaultBom.ParentObjectType,
            defaultBom.ChildObjectType,
            defaultBom,
            null);
    }

    internal static void ValidateStructuralRelationshipType(
        ResolvedModelPackageContext modelContext,
        string? structuralRelationshipType)
    {
        if (string.IsNullOrWhiteSpace(structuralRelationshipType))
        {
            return;
        }

        var normalized = NormalizeKey(structuralRelationshipType);
        var exists = modelContext.Ontology.BomRelationships.Any(item => item.NormalizedRelationshipType == normalized)
            || modelContext.Ontology.RelationshipTypes.Any(item => item.NormalizedRelationshipType == normalized);
        if (!exists)
        {
            throw new RequestValidationException($"Structural relationship type '{structuralRelationshipType}' is not defined by the active model package.");
        }
    }

    private static ResolvedStructuralRelationship ResolveExplicit(
        ResolvedModelPackageContext modelContext,
        string structuralRelationshipType,
        string? parentObjectType,
        string? childObjectType)
    {
        var normalized = NormalizeKey(structuralRelationshipType);
        var bomMatches = modelContext.Ontology.BomRelationships
            .Where(item => item.NormalizedRelationshipType == normalized)
            .ToList();
        if (bomMatches.Count > 0)
        {
            var bom = SelectBestMatch(
                bomMatches,
                parentObjectType,
                childObjectType,
                item => item.ParentObjectType,
                item => item.ChildObjectType)
                ?? throw new RequestValidationException(
                    $"Structural relationship type '{structuralRelationshipType}' does not match the parent/child object types configured on this mapping.");
            ValidateEndpointTypes(bom.ParentObjectType, bom.ChildObjectType, parentObjectType, childObjectType, bom.RelationshipType);
            return new ResolvedStructuralRelationship(
                bom.RelationshipType,
                bom.ParentObjectType,
                bom.ChildObjectType,
                bom,
                null);
        }

        var semanticMatches = modelContext.Ontology.RelationshipTypes
            .Where(item => item.NormalizedRelationshipType == normalized)
            .ToList();
        if (semanticMatches.Count > 0)
        {
            var semantic = SelectBestMatch(
                semanticMatches,
                parentObjectType,
                childObjectType,
                item => item.FromObjectType,
                item => item.ToObjectType)
                ?? throw new RequestValidationException(
                    $"Structural relationship type '{structuralRelationshipType}' does not match the parent/child object types configured on this mapping.");
            ValidateEndpointTypes(semantic.FromObjectType, semantic.ToObjectType, parentObjectType, childObjectType, semantic.RelationshipType);
            return new ResolvedStructuralRelationship(
                semantic.RelationshipType,
                semantic.FromObjectType,
                semantic.ToObjectType,
                null,
                semantic);
        }

        throw new RequestValidationException($"Structural relationship type '{structuralRelationshipType}' is not defined by the active model package.");
    }

    private static string? ResolveEndpointObjectType(ImportMappingVersion mapping, string sourceColumnHeader)
    {
        var normalizedHeader = NormalizeKey(sourceColumnHeader);
        var identityMapping = mapping.ColumnMappings.FirstOrDefault(item =>
            item.IsIdentityField
            && item.NormalizedSourceColumn == normalizedHeader);
        return identityMapping?.CanonicalObjectType;
    }

    private static void ValidateEndpointTypes(
        string expectedParentObjectType,
        string expectedChildObjectType,
        string? parentObjectType,
        string? childObjectType,
        string relationshipType)
    {
        if (parentObjectType is not null
            && !string.Equals(expectedParentObjectType, parentObjectType, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Structural relationship '{relationshipType}' expects parent object type '{expectedParentObjectType}', but mapping parent column resolves to '{parentObjectType}'.");
        }

        if (childObjectType is not null
            && !string.Equals(expectedChildObjectType, childObjectType, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Structural relationship '{relationshipType}' expects child object type '{expectedChildObjectType}', but mapping child column resolves to '{childObjectType}'.");
        }
    }

    private static T? SelectBestMatch<T>(
        IReadOnlyCollection<T> matches,
        string? parentObjectType,
        string? childObjectType,
        Func<T, string> parentSelector,
        Func<T, string> childSelector)
        where T : class
    {
        if (parentObjectType is not null && childObjectType is not null)
        {
            return matches.FirstOrDefault(item =>
                string.Equals(parentSelector(item), parentObjectType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(childSelector(item), childObjectType, StringComparison.OrdinalIgnoreCase));
        }

        return matches.Count == 1 ? matches.First() : null;
    }

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
}
