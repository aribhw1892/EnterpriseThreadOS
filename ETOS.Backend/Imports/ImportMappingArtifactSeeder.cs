using ETOS.Backend.Artifacts;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Imports.MappingSuggestions;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Imports;

public interface IImportMappingArtifactSeeder
{
    Task<ImportMappingPlatformArtifacts> EnsurePlatformArtifactsAsync(ActiveTenantContext context, CancellationToken cancellationToken);
}

public sealed record ImportMappingPlatformArtifacts(
    PlatformArtifactVersion PromptTemplate,
    PlatformArtifactVersion OutputSchema);

public sealed class ImportMappingArtifactSeeder(EnterpriseThreadDbContext dbContext) : IImportMappingArtifactSeeder
{
    public async Task<ImportMappingPlatformArtifacts> EnsurePlatformArtifactsAsync(
        ActiveTenantContext context,
        CancellationToken cancellationToken)
    {
        var prompt = await EnsureArtifactVersionAsync(
            context,
            "PromptTemplateVersion",
            "platform-import-mapping",
            "platform-import-mapping-v1",
            "Platform import mapping assistant prompt template",
            BuildPromptTemplatePayload(),
            cancellationToken);
        var outputSchema = await EnsureArtifactVersionAsync(
            context,
            "OutputSchemaVersion",
            "import-mapping-suggestion-schema",
            "import-mapping-suggestion-v1",
            "Import mapping suggestion output schema",
            MappingSuggestionOutputSchema.Json,
            cancellationToken);

        return new ImportMappingPlatformArtifacts(prompt, outputSchema);
    }

    private async Task<PlatformArtifactVersion> EnsureArtifactVersionAsync(
        ActiveTenantContext context,
        string artifactType,
        string artifactName,
        string versionLabel,
        string summary,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeKey(artifactType);
        var artifact = await dbContext.Artifacts.SingleOrDefaultAsync(
            item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == normalizedType
                && item.Name == artifactName,
            cancellationToken);

        if (artifact is null)
        {
            artifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                ArtifactType = artifactType,
                NormalizedArtifactType = normalizedType,
                Name = artifactName,
                Description = summary,
                OwnerUserId = context.UserId,
                LifecycleState = ArtifactLifecycleState.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Artifacts.Add(artifact);
        }

        var normalizedVersion = NormalizeKey(versionLabel);
        var version = await dbContext.ArtifactVersions.SingleOrDefaultAsync(
            item => item.ArtifactId == artifact.Id
                && item.TenantId == context.TenantId
                && item.NormalizedVersionLabel == normalizedVersion,
            cancellationToken);

        if (version is null)
        {
            version = new ArtifactVersion
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                ArtifactId = artifact.Id,
                VersionLabel = versionLabel,
                NormalizedVersionLabel = normalizedVersion,
                PayloadJson = payloadJson,
                ReadinessState = ArtifactReadinessState.Published,
                CreatedByUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.ArtifactVersions.Add(version);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!string.Equals(version.PayloadJson, payloadJson, StringComparison.Ordinal))
        {
            version.PayloadJson = payloadJson;
            version.ReadinessState = ArtifactReadinessState.Published;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PlatformArtifactVersion(
            artifact.Id,
            version.Id,
            artifactType,
            versionLabel,
            payloadJson);
    }

    private static string BuildPromptTemplatePayload()
        => """
           {
             "body": "You are an import mapping assistant for EnterpriseThreadOS. Analyze CSV headers and sample rows against the governed ontology context and the structured-input allow-lists. Map only to objectTypes[].key / attributes[].attributeKey / lifecycleStates[].key from governed context (same values as allowedObjectTypes, allowedAttributes, and allowedLifecycleKeys). Never invent keys. Never copy sourceColumn into canonicalAttributeKey unless that exact string is an allowed attributeKey for the chosen object type. If no ontology match exists, set canonicalAttributeKey to null and use low confidence. Prefer tool output summaries when they already use valid ontology keys. Return one JSON data object with columnSuggestions and lifecycleSuggestions arrays filled with your mappings. Do not return a JSON Schema document."
           }
           """;

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
}
