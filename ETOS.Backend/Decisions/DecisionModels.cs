using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Decisions;

public sealed class DecisionVote : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DecisionArtifactId { get; set; }
    public Guid UserId { get; set; }
    public DecisionVoteKind Vote { get; set; }
    public string? Comment { get; set; }
    public decimal? Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DecisionComment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DecisionArtifactId { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
