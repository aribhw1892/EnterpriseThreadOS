using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernedChat;

public interface IGovernedChatArtifactSeeder
{
    Task<GovernedChatPlatformArtifacts> EnsurePlatformArtifactsAsync(ActiveTenantContext context, CancellationToken cancellationToken);
}

public sealed record GovernedChatPlatformArtifacts(
    PlatformArtifactVersion PromptTemplate,
    PlatformArtifactVersion ChatAnswerSchema,
    PlatformArtifactVersion DraftQueryIntentSchema,
    PlatformArtifactVersion DraftDashboardSchema,
    PlatformArtifactVersion DraftReportSchema);

public sealed record PlatformArtifactVersion(
    Guid ArtifactId,
    Guid VersionId,
    string ArtifactType,
    string VersionLabel,
    string PayloadJson);

public sealed class GovernedChatArtifactSeeder(EnterpriseThreadDbContext dbContext) : IGovernedChatArtifactSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GovernedChatPlatformArtifacts> EnsurePlatformArtifactsAsync(ActiveTenantContext context, CancellationToken cancellationToken)
    {
        var prompt = await EnsureArtifactVersionAsync(
            context,
            "PromptTemplateVersion",
            "platform-governed-chat",
            "platform-governed-chat-v1",
            "Platform governed chat prompt template",
            BuildPromptTemplatePayload(),
            cancellationToken);
        var chatAnswer = await EnsureArtifactVersionAsync(
            context,
            "OutputSchemaVersion",
            "chat-answer-schema",
            "chat-answer-v1",
            "Governed chat answer output schema",
            BuildChatAnswerSchemaPayload(),
            cancellationToken);
        var draftQueryIntent = await EnsureArtifactVersionAsync(
            context,
            "OutputSchemaVersion",
            "draft-query-intent-schema",
            "draft-query-intent-v1",
            "Draft query intent output schema",
            BuildDraftQueryIntentSchemaPayload(),
            cancellationToken);
        var draftDashboard = await EnsureArtifactVersionAsync(
            context,
            "OutputSchemaVersion",
            "draft-dashboard-schema",
            "draft-dashboard-v1",
            "Draft dashboard output schema",
            BuildDraftDashboardSchemaPayload(),
            cancellationToken);
        var draftReport = await EnsureArtifactVersionAsync(
            context,
            "OutputSchemaVersion",
            "draft-report-schema",
            "draft-report-v1",
            "Draft report output schema",
            BuildDraftReportSchemaPayload(),
            cancellationToken);

        return new GovernedChatPlatformArtifacts(prompt, chatAnswer, draftQueryIntent, draftDashboard, draftReport);
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
        var normalizedName = NormalizeKey(artifactName);
        var normalizedVersion = NormalizeKey(versionLabel);

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
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var version = await dbContext.ArtifactVersions.SingleOrDefaultAsync(
            item => item.TenantId == context.TenantId
                && item.ArtifactId == artifact.Id
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
                Summary = summary,
                PayloadJson = payloadJson,
                ReadinessState = ArtifactReadinessState.Published,
                CompatibilityStatus = ArtifactCompatibilityStatus.Compatible,
                CompatibilitySummary = "Platform seeded governed chat artifact.",
                PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable,
                CreatedByUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                PublishedByUserId = context.UserId,
                PublishedAt = DateTimeOffset.UtcNow,
                PublishSummary = "Platform seed publish."
            };
            dbContext.ArtifactVersions.Add(version);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PlatformArtifactVersion(artifact.Id, version.Id, artifactType, versionLabel, version.PayloadJson ?? payloadJson);
    }

    private static string BuildPromptTemplatePayload()
    {
        return Serialize(new
        {
            template = """
                You are EnterpriseThreadOS governed chat. Answer using only the LLM-visible context block.
                Query intent: {{intentKey}}
                User question: {{question}}
                LLM-visible context:
                {{contextJson}}
                """
        });
    }

    private static string BuildChatAnswerSchemaPayload()
    {
        return Serialize(new
        {
            type = "object",
            required = new[] { "answer", "evidence", "confidence" },
            properties = new
            {
                answer = new { type = "string" },
                evidence = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        required = new[] { "contextId", "contextType", "safeSummary" },
                        properties = new
                        {
                            contextId = new { type = "string" },
                            contextType = new { type = "string" },
                            safeSummary = new { type = "string" }
                        }
                    }
                },
                confidence = new
                {
                    type = "object",
                    required = new[] { "overall" },
                    properties = new
                    {
                        overall = new { type = "number" },
                        notes = new { type = "string" }
                    }
                }
            }
        });
    }

    private static string BuildDraftQueryIntentSchemaPayload()
    {
        return Serialize(new
        {
            type = "object",
            required = new[] { "intentKey", "name", "summary", "createdFromChat" },
            properties = new
            {
                intentKey = new { type = "string" },
                name = new { type = "string" },
                summary = new { type = "string" },
                createdFromChat = new { type = "boolean" }
            }
        });
    }

    private static string BuildDraftDashboardSchemaPayload()
    {
        return Serialize(new
        {
            type = "object",
            required = new[] { "name", "summary", "widgets", "createdFromChat" },
            properties = new
            {
                name = new { type = "string" },
                summary = new { type = "string" },
                widgets = new { type = "array" },
                createdFromChat = new { type = "boolean" }
            }
        });
    }

    private static string BuildDraftReportSchemaPayload()
    {
        return Serialize(new
        {
            type = "object",
            required = new[] { "name", "summary", "sections", "createdFromChat" },
            properties = new
            {
                name = new { type = "string" },
                summary = new { type = "string" },
                sections = new { type = "array" },
                createdFromChat = new { type = "boolean" }
            }
        });
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
}
