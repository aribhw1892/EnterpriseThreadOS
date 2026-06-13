using ETOS.Backend.Tenancy;

namespace ETOS.Backend.GovernedChat;

public sealed class GovernedChatSession : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Title { get; set; }
    public Guid StartedByUserId { get; set; }
    public Guid? StartGraphNodeId { get; set; }
    public Guid? DocumentArtifactId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTurnAt { get; set; }
    public List<GovernedChatTurn> Turns { get; set; } = [];
}

public sealed class GovernedChatTurn : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public GovernedChatSession? Session { get; set; }
    public required string UserMessage { get; set; }
    public required string AssistantSafeSummary { get; set; }
    public Guid RetrievalRunId { get; set; }
    public Guid ContextPackageId { get; set; }
    public Guid? AiTraceRecordId { get; set; }
    public Guid PromptTemplateArtifactId { get; set; }
    public Guid PromptTemplateVersionId { get; set; }
    public Guid OutputSchemaArtifactId { get; set; }
    public Guid OutputSchemaVersionId { get; set; }
    public required string GeneratedOutputJson { get; set; }
    public required string EvidenceJson { get; set; }
    public required string ConfidenceJson { get; set; }
    public Guid? DraftArtifactId { get; set; }
    public Guid? DraftArtifactVersionId { get; set; }
    public ChatDraftArtifactKind? DraftArtifactKind { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
