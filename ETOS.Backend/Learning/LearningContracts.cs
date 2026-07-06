namespace ETOS.Backend.Learning;

public static class LearningPermissions
{
    public const string Read = "learning_signals.read";
    public const string Admin = "learning.admin";
}

public static class LearningArtifactTypes
{
    public const string LearningSignal = "LearningSignalArtifact";
    public const string LearningPolicy = "LearningPolicyVersion";
    public const string LearningModel = "LearningModelVersion";
}

public sealed class LearningSignalRollupOptions
{
    public const string SectionName = "LearningSignals:Rollup";

    public int MinOccurrences { get; set; } = 3;

    public int WindowDays { get; set; } = 30;
}

public sealed record LearningSignalSummaryResponse(
    Guid ArtifactId,
    string PatternKey,
    int OccurrenceCount,
    string Summary,
    string Status);

public sealed record LearningPlaceholderArtifactResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ArtifactType,
    string Name,
    string Status);
