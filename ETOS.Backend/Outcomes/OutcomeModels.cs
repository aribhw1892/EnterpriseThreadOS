using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Outcomes;

public sealed class OutcomeCheckRun : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DecisionArtifactId { get; set; }
    public required string CheckType { get; set; }
    public required string ExpectedOutcome { get; set; }
    public required string ActualOutcome { get; set; }
    public OutcomeCheckStatus OutcomeStatus { get; set; }
    public decimal? OutcomeConfidence { get; set; }
    public DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.UtcNow;
    public required string EvidenceSummary { get; set; }
    public Guid RecordedByUserId { get; set; }
    public Guid? RecommendationArtifactId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
