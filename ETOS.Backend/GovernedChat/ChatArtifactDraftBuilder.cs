using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;

namespace ETOS.Backend.GovernedChat;

public interface IChatArtifactDraftBuilder
{
    Task<GovernedChatDraftArtifactResponse> CreateDraftAsync(
        ActiveTenantContext context,
        ChatDraftArtifactKind draftKind,
        GovernedChatTurn turn,
        PlatformArtifactVersion outputSchema,
        PlatformArtifactVersion promptTemplate,
        string draftOutputJson,
        CancellationToken cancellationToken);
}

public sealed class ChatArtifactDraftBuilder(EnterpriseThreadDbContext dbContext) : IChatArtifactDraftBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GovernedChatDraftArtifactResponse> CreateDraftAsync(
        ActiveTenantContext context,
        ChatDraftArtifactKind draftKind,
        GovernedChatTurn turn,
        PlatformArtifactVersion outputSchema,
        PlatformArtifactVersion promptTemplate,
        string draftOutputJson,
        CancellationToken cancellationToken)
    {
        var artifactType = draftKind switch
        {
            ChatDraftArtifactKind.QueryIntent => "QueryIntentVersion",
            ChatDraftArtifactKind.Dashboard => "DashboardVersion",
            ChatDraftArtifactKind.Report => "ReportVersion",
            _ => throw new RequestValidationException("Unsupported draft artifact kind.")
        };

        var versionLabel = $"chat-draft-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = artifactType,
            NormalizedArtifactType = artifactType.ToUpperInvariant(),
            Name = $"{artifactType} from chat {turn.Id:N}",
            Description = "Draft artifact generated from governed chat.",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var payload = EnrichDraftPayload(
            draftOutputJson,
            turn,
            outputSchema.VersionLabel,
            promptTemplate.VersionLabel);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = versionLabel,
            NormalizedVersionLabel = versionLabel.ToUpperInvariant(),
            Summary = "Chat-generated draft awaiting readiness review.",
            PayloadJson = payload,
            ReadinessState = ArtifactReadinessState.Draft,
            CompatibilityStatus = ArtifactCompatibilityStatus.Unknown,
            PolicyRiskStatus = ArtifactPolicyRiskStatus.NotEvaluated,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        dbContext.ArtifactDependencies.AddRange(
            CreateDependency(context.TenantId, version.Id, promptTemplate.ArtifactId, promptTemplate.VersionId),
            CreateDependency(context.TenantId, version.Id, outputSchema.ArtifactId, outputSchema.VersionId));

        if (turn.AiTraceRecordId is not null)
        {
            dbContext.ArtifactRelationships.Add(new ArtifactRelationship
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                SourceArtifactId = artifact.Id,
                TargetArtifactId = promptTemplate.ArtifactId,
                RelationshipType = ArtifactRelationshipType.DerivedFrom,
                Description = $"Created from AI Trace {turn.AiTraceRecordId}.",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        dbContext.ArtifactRelationships.Add(new ArtifactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceArtifactId = artifact.Id,
            TargetArtifactId = outputSchema.ArtifactId,
            RelationshipType = ArtifactRelationshipType.Uses,
            Description = "Uses pinned output schema version.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GovernedChatDraftArtifactResponse(
            artifact.Id,
            version.Id,
            artifactType,
            versionLabel,
            version.ReadinessState);
    }

    private static ArtifactDependency CreateDependency(Guid tenantId, Guid dependentVersionId, Guid requiredArtifactId, Guid requiredVersionId)
    {
        return new ArtifactDependency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DependentVersionId = dependentVersionId,
            RequiredArtifactId = requiredArtifactId,
            RequiredVersionId = requiredVersionId,
            DependencyKind = ArtifactDependencyKind.DependsOn,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string EnrichDraftPayload(
        string draftOutputJson,
        GovernedChatTurn turn,
        string outputSchemaVersionLabel,
        string promptTemplateVersionLabel)
    {
        var node = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(draftOutputJson, JsonOptions)
            ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var enriched = node.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        enriched["aiTraceRecordId"] = JsonSerializer.SerializeToElement(turn.AiTraceRecordId);
        enriched["retrievalRunId"] = JsonSerializer.SerializeToElement(turn.RetrievalRunId);
        enriched["promptTemplateVersionLabel"] = JsonSerializer.SerializeToElement(promptTemplateVersionLabel);
        enriched["outputSchemaVersionLabel"] = JsonSerializer.SerializeToElement(outputSchemaVersionLabel);
        enriched["createdFromChat"] = JsonSerializer.SerializeToElement(true);
        return JsonSerializer.Serialize(enriched, JsonOptions);
    }
}
