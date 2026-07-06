using System.Text.Json.Serialization;

namespace ETOS.Backend.Outcomes;

public static class OutcomePermissions
{
    public const string Read = "outcomes.read";
    public const string Record = "outcomes.record";
    public const string Admin = "outcomes.admin";
}

public static class OutcomeTaxonomyArtifactTypes
{
    public const string OutcomeTaxonomy = "OutcomeTaxonomyVersion";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutcomeCheckStatus
{
    Pending = 0,
    Successful = 1,
    Failed = 2,
    Partial = 3
}

public sealed record OutcomeTaxonomyDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string TaxonomyKey,
    IReadOnlyCollection<string> Categories,
    string ArtifactReadinessState);

public sealed record CreateOutcomeTaxonomyRequest(
    string Name,
    string? Description,
    string TaxonomyKey,
    IReadOnlyCollection<string> Categories);

public sealed record CreateOutcomeTaxonomyVersionRequest(
    string VersionLabel,
    string? Summary,
    string TaxonomyKey,
    IReadOnlyCollection<string> Categories);

public sealed record CreateOutcomeTaxonomyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record OutcomeCheckRunResponse(
    Guid Id,
    Guid DecisionArtifactId,
    string CheckType,
    string ExpectedOutcome,
    string ActualOutcome,
    OutcomeCheckStatus OutcomeStatus,
    decimal? OutcomeConfidence,
    DateTimeOffset MeasuredAt,
    string EvidenceSummary,
    Guid RecordedByUserId);

public sealed record RecordManualOutcomeRequest(
    string CheckType,
    string ExpectedOutcome,
    string ActualOutcome,
    OutcomeCheckStatus OutcomeStatus,
    decimal? OutcomeConfidence,
    string? EvidenceSummary,
    Guid? RecommendationArtifactId);

public sealed record RecordManualOutcomeResponse(
    Guid OutcomeCheckRunId,
    Guid DecisionArtifactId,
    Guid DecisionVersionId,
    OutcomeCheckRunResponse OutcomeCheckRun);

public sealed record OutcomeDevelopmentSeedResult(
    Guid TaxonomyArtifactId,
    Guid TaxonomyVersionId);
