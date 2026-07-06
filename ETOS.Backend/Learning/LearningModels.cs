using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Learning;

public sealed class DecisionLearningEvidence : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? DecisionArtifactId { get; set; }
    public required string PatternKey { get; set; }
    public required string SourceType { get; set; }
    public required string OutcomeKey { get; set; }
    public required string EvidenceSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
