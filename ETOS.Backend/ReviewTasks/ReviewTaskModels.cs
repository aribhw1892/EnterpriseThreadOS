using ETOS.Backend.Tenancy;

namespace ETOS.Backend.ReviewTasks;

public sealed class ReviewTaskComment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TaskArtifactId { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ReviewTaskChainLink : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BlockedTaskArtifactId { get; set; }
    public Guid BlockingTaskArtifactId { get; set; }
    public ReviewTaskChainReason ChainReason { get; set; }
    public ReviewTaskBlockingCondition BlockingCondition { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}
