using ETOS.Backend.Identity;
using ETOS.Backend.Ontology;

namespace ETOS.Backend.Imports;

internal static class ImportStructuralImportHelper
{
    internal sealed record StructuralHeaders(
        string ParentHeader,
        string ChildHeader,
        string? QuantityHeader,
        string? UnitHeader,
        string? UsageHeader);

    internal sealed record StructuralComparisonSide(string Label, IReadOnlyCollection<string> Aliases);

    internal sealed record StructuralLine(
        string Side,
        string? ParentIdentity,
        string? ChildIdentity,
        string Key,
        string? Quantity,
        string? Unit,
        string? UsageReference,
        int RowNumber);

    internal sealed record StructuralComparisonResult(
        StructuralSideSummary PrimarySide,
        StructuralSideSummary SecondarySide,
        IReadOnlyCollection<string> MissingInPrimary,
        IReadOnlyCollection<string> MissingInSecondary,
        IReadOnlyCollection<string> QuantityMismatches,
        IReadOnlyCollection<string> UsageReferenceMismatches,
        IReadOnlyCollection<string> UnresolvedIdentities);

    internal sealed record StructuralSideSummary(int LineCount);

    internal static StructuralHeaders? TryResolveStructuralHeaders(
        IReadOnlyCollection<string> headers,
        ModelPackageImportProfile profile)
    {
        var parentHeader = FindHeader(headers, profile.ParentColumnSynonyms);
        var childHeader = FindHeader(headers, profile.ChildColumnSynonyms);
        if (parentHeader is null || childHeader is null)
        {
            return null;
        }

        return new StructuralHeaders(
            parentHeader,
            childHeader,
            FindHeader(headers, profile.QuantityColumnSynonyms),
            FindHeader(headers, profile.UnitColumnSynonyms),
            FindHeader(headers, profile.UsageColumnSynonyms));
    }

    internal static StructuralComparisonResult BuildStructuralComparison(
        ParsedImportFile parsed,
        ModelPackageImportProfile profile)
    {
        var sideHeader = FindHeader(parsed.Headers, profile.ComparisonSideColumnSynonyms)
            ?? throw new RequestValidationException("Structural comparison requires a side column configured by the active model package.");
        var parentHeader = FindHeader(parsed.Headers, profile.ParentColumnSynonyms)
            ?? throw new RequestValidationException("Structural comparison requires a parent item column configured by the active model package.");
        var childHeader = FindHeader(parsed.Headers, profile.ChildColumnSynonyms)
            ?? throw new RequestValidationException("Structural comparison requires a child item column configured by the active model package.");
        var quantityHeader = FindHeader(parsed.Headers, profile.QuantityColumnSynonyms);
        var unitHeader = FindHeader(parsed.Headers, profile.UnitColumnSynonyms);
        var usageHeader = FindHeader(parsed.Headers, profile.UsageColumnSynonyms);
        var sides = ResolveComparisonSides(profile);

        var lines = parsed.Rows
            .Select((row, index) => ToStructuralLine(row, index + 2, sideHeader, parentHeader, childHeader, quantityHeader, unitHeader, usageHeader, sides))
            .ToList();
        var primary = lines.Where(line => string.Equals(line.Side, sides[0].Label, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(line => line.Key, StringComparer.OrdinalIgnoreCase);
        var secondary = lines.Where(line => string.Equals(line.Side, sides[1].Label, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(line => line.Key, StringComparer.OrdinalIgnoreCase);
        var missingInPrimary = secondary.Keys.Except(primary.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var missingInSecondary = primary.Keys.Except(secondary.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var quantityMismatches = primary.Keys.Intersect(secondary.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => !string.Equals(primary[key].Quantity, secondary[key].Quantity, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(primary[key].Unit, secondary[key].Unit, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var usageReferenceMismatches = primary.Keys.Intersect(secondary.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => !string.Equals(primary[key].UsageReference, secondary[key].UsageReference, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unresolved = lines
            .Where(line => string.IsNullOrWhiteSpace(line.ParentIdentity) || string.IsNullOrWhiteSpace(line.ChildIdentity))
            .Select(line => $"row:{line.RowNumber}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StructuralComparisonResult(
            new StructuralSideSummary(primary.Count),
            new StructuralSideSummary(secondary.Count),
            missingInPrimary,
            missingInSecondary,
            quantityMismatches,
            usageReferenceMismatches,
            unresolved);
    }

    internal static Dictionary<string, string?> BuildRelationshipAttributes(
        IReadOnlyDictionary<string, string?> row,
        StructuralHeaders headers,
        BomRelationshipDefinition bomRelationship)
    {
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(bomRelationship.QuantityAttributeKey) && headers.QuantityHeader is not null)
        {
            attributes[bomRelationship.QuantityAttributeKey] = GetRowValue(row, headers.QuantityHeader);
        }

        if (!string.IsNullOrWhiteSpace(bomRelationship.UnitAttributeKey) && headers.UnitHeader is not null)
        {
            attributes[bomRelationship.UnitAttributeKey] = GetRowValue(row, headers.UnitHeader);
        }

        var usageHeader = headers.UsageHeader;
        if (usageHeader is not null)
        {
            var usageValue = GetRowValue(row, usageHeader);
            if (!string.IsNullOrWhiteSpace(bomRelationship.FindNumberAttributeKey))
            {
                attributes[bomRelationship.FindNumberAttributeKey] = usageValue;
            }

            if (!string.IsNullOrWhiteSpace(bomRelationship.ReferenceDesignatorAttributeKey))
            {
                attributes[bomRelationship.ReferenceDesignatorAttributeKey] = usageValue;
            }
        }

        return attributes;
    }

    internal static Dictionary<string, string?> BuildIdentityAttributes(
        string identityValue,
        BomRelationshipDefinition bomRelationship,
        ResolvedModelPackageContext modelContext,
        bool isParent)
    {
        var objectType = isParent ? bomRelationship.ParentObjectType : bomRelationship.ChildObjectType;
        return BuildOntologyFallbackIdentityAttributes(identityValue, objectType, modelContext);
    }

    internal static Dictionary<string, string?> BuildStructuralIdentityAttributes(
        string identityValue,
        string sourceColumnHeader,
        string objectType,
        ImportMappingVersion mapping,
        ResolvedModelPackageContext modelContext)
    {
        var normalizedHeader = NormalizeKey(sourceColumnHeader);
        var identityMapping = mapping.ColumnMappings.FirstOrDefault(item =>
            item.IsIdentityField
            && item.NormalizedSourceColumn == normalizedHeader
            && string.Equals(item.CanonicalObjectType, objectType, StringComparison.OrdinalIgnoreCase));
        if (identityMapping?.CanonicalAttributeKey is not null)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [identityMapping.CanonicalAttributeKey] = identityValue
            };
        }

        return BuildOntologyFallbackIdentityAttributes(identityValue, objectType, modelContext);
    }

    private static Dictionary<string, string?> BuildOntologyFallbackIdentityAttributes(
        string identityValue,
        string objectType,
        ResolvedModelPackageContext modelContext)
    {
        var objectTypeDefinition = modelContext.Ontology.ObjectTypes
            .FirstOrDefault(item => string.Equals(item.Key, objectType, StringComparison.OrdinalIgnoreCase));
        var identityFields = ParseIdentityFields(objectTypeDefinition?.VersionIdentityFieldsJson);
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (identityFields.Count == 0)
        {
            var fallbackAttribute = modelContext.AttributeSchema.Attributes
                .FirstOrDefault(item => string.Equals(item.AppliesToObjectType, objectType, StringComparison.OrdinalIgnoreCase) && item.IsRequired)
                ?? modelContext.AttributeSchema.Attributes.FirstOrDefault(item => string.Equals(item.AppliesToObjectType, objectType, StringComparison.OrdinalIgnoreCase));
            if (fallbackAttribute is not null)
            {
                attributes[fallbackAttribute.AttributeKey] = identityValue;
            }

            return attributes;
        }

        attributes[identityFields[0]] = identityValue;
        return attributes;
    }

    private static StructuralLine ToStructuralLine(
        IReadOnlyDictionary<string, string?> row,
        int rowNumber,
        string sideHeader,
        string parentHeader,
        string childHeader,
        string? quantityHeader,
        string? unitHeader,
        string? usageHeader,
        IReadOnlyList<StructuralComparisonSide> sides)
    {
        var side = GetRowValue(row, sideHeader);
        var normalizedSide = NormalizeLoose(side);
        var canonicalSide = sides.FirstOrDefault(candidate =>
                candidate.Aliases.Any(alias => normalizedSide.Contains(NormalizeLoose(alias), StringComparison.Ordinal))
                || string.Equals(candidate.Label, side, StringComparison.OrdinalIgnoreCase))
            ?? throw new RequestValidationException("Structural comparison side values must match labels configured by the active model package.");
        var parent = GetRowValue(row, parentHeader);
        var child = GetRowValue(row, childHeader);
        return new StructuralLine(
            canonicalSide.Label,
            parent,
            child,
            $"{parent}|{child}",
            quantityHeader is null ? null : GetRowValue(row, quantityHeader),
            unitHeader is null ? null : GetRowValue(row, unitHeader),
            usageHeader is null ? null : GetRowValue(row, usageHeader),
            rowNumber);
    }

    private static IReadOnlyList<StructuralComparisonSide> ResolveComparisonSides(ModelPackageImportProfile profile)
    {
        if (profile.ComparisonSides.Count >= 2)
        {
            return profile.ComparisonSides
                .Select(side => new StructuralComparisonSide(side.Label, side.Aliases))
                .Take(2)
                .ToList();
        }

        throw new RequestValidationException("Active model package import profile must define two structural comparison sides.");
    }

    private static IReadOnlyList<string> ParseIdentityFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    internal static string? FindHeader(IReadOnlyCollection<string> headers, IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var normalizedCandidates = candidates.Select(NormalizeLoose).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return headers.FirstOrDefault(header => normalizedCandidates.Contains(NormalizeLoose(header)));
    }

    internal static string GetRowValue(IReadOnlyDictionary<string, string?> row, string header)
    {
        return row.TryGetValue(header, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private static string NormalizeLoose(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
}
