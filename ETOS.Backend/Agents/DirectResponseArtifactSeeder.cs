using ETOS.Backend.Artifacts;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Agents;

public interface IDirectResponseArtifactSeeder
{
    Task<DirectResponsePlatformArtifacts> EnsurePlatformArtifactsAsync(
        ActiveTenantContext context,
        CancellationToken cancellationToken);
}

public sealed record DirectResponsePlatformArtifacts(
    PlatformArtifactVersion PromptTemplate,
    PlatformArtifactVersion OutputSchema);

public sealed class DirectResponseArtifactSeeder(EnterpriseThreadDbContext dbContext) : IDirectResponseArtifactSeeder
{
    public async Task<DirectResponsePlatformArtifacts> EnsurePlatformArtifactsAsync(
        ActiveTenantContext context,
        CancellationToken cancellationToken)
    {
        var prompt = await EnsureArtifactVersionAsync(
            context,
            "PromptTemplateVersion",
            "platform-direct-response",
            "platform-direct-response-v1",
            "Platform direct-response agent prompt template",
            BuildPromptTemplatePayload(),
            cancellationToken);
        var outputSchema = await EnsureArtifactVersionAsync(
            context,
            "OutputSchemaVersion",
            "direct-response-schema",
            "direct-response-v1",
            "Direct-response agent output schema",
            DirectResponseOutputSchema.Json,
            cancellationToken);

        return new DirectResponsePlatformArtifacts(prompt, outputSchema);
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
             "body": "You are a governed EnterpriseThreadOS assistant. Use the user query text and any governed context summary provided below. Respond concisely in structured JSON that matches the output schema. Follow the agent display intent; do not perform import mapping or graph analysis unless the user explicitly asks for it."
           }
           """;

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
}
