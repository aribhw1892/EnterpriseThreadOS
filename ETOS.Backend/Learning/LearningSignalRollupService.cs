using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Decisions;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Learning;

public interface ILearningEvidenceEmitter
{
    Task EmitDecisionEvidenceAsync(
        Guid tenantId,
        Guid decisionArtifactId,
        DecisionPayloadParser.DecisionPayloadDocument payload,
        bool isFinalized,
        CancellationToken cancellationToken);
}

public interface ILearningSignalRollupService
{
    Task EvaluateAsync(
        Guid tenantId,
        Guid ownerUserId,
        DecisionPayloadParser.DecisionPayloadDocument payload,
        CancellationToken cancellationToken);
}

public sealed class LearningEvidenceEmitter(EnterpriseThreadDbContext dbContext) : ILearningEvidenceEmitter
{
    public async Task EmitDecisionEvidenceAsync(
        Guid tenantId,
        Guid decisionArtifactId,
        DecisionPayloadParser.DecisionPayloadDocument payload,
        bool isFinalized,
        CancellationToken cancellationToken)
    {
        if (!isFinalized)
        {
            return;
        }

        var patternKey = BuildPatternKey(payload);
        dbContext.DecisionLearningEvidence.Add(new DecisionLearningEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DecisionArtifactId = decisionArtifactId,
            PatternKey = patternKey,
            SourceType = payload.SourceType ?? "manual",
            OutcomeKey = payload.OutcomeKey ?? string.Empty,
            EvidenceSummary = $"Decision finalized with outcome '{payload.OutcomeKey}' for review type '{payload.ReviewTaskType}'.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string BuildPatternKey(DecisionPayloadParser.DecisionPayloadDocument payload)
        => $"{payload.SourceType}:{payload.OutcomeKey}:{payload.ReviewTaskType}".ToLowerInvariant();
}

public sealed class LearningSignalRollupService(
    EnterpriseThreadDbContext dbContext,
    IOptions<LearningSignalRollupOptions> options) : ILearningSignalRollupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EvaluateAsync(
        Guid tenantId,
        Guid ownerUserId,
        DecisionPayloadParser.DecisionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        var rollupOptions = options.Value;
        var patternKey = LearningEvidenceEmitter.BuildPatternKey(payload);
        var windowStart = DateTimeOffset.UtcNow.AddDays(-rollupOptions.WindowDays);
        var occurrenceCount = await dbContext.DecisionLearningEvidence
            .AsNoTracking()
            .CountAsync(
                evidence => evidence.TenantId == tenantId
                    && evidence.PatternKey == patternKey
                    && evidence.CreatedAt >= windowStart,
                cancellationToken);

        if (occurrenceCount < rollupOptions.MinOccurrences)
        {
            return;
        }

        var normalizedType = LearningArtifactTypes.LearningSignal.ToUpperInvariant();
        var existingSignals = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == tenantId && artifact.NormalizedArtifactType == normalizedType)
            .Select(artifact => artifact.Id)
            .ToListAsync(cancellationToken);

        foreach (var signalArtifactId in existingSignals)
        {
            var latestVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Where(version => version.ArtifactId == signalArtifactId)
                .OrderByDescending(version => version.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestVersion?.PayloadJson is null)
            {
                continue;
            }

            using var document = JsonDocument.Parse(latestVersion.PayloadJson);
            if (document.RootElement.TryGetProperty("patternKey", out var existingPattern)
                && string.Equals(existingPattern.GetString(), patternKey, StringComparison.OrdinalIgnoreCase)
                && document.RootElement.TryGetProperty("status", out var status)
                && string.Equals(status.GetString(), "active", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var sourceDecisionIds = await dbContext.DecisionLearningEvidence
            .AsNoTracking()
            .Where(evidence => evidence.TenantId == tenantId && evidence.PatternKey == patternKey && evidence.CreatedAt >= windowStart)
            .OrderByDescending(evidence => evidence.CreatedAt)
            .Select(evidence => evidence.DecisionArtifactId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Take(20)
            .ToListAsync(cancellationToken);

        var signalPayload = JsonSerializer.Serialize(new
        {
            patternKey,
            occurrenceCount,
            sourceDecisionIds,
            summary = $"Repeated governance pattern detected for '{patternKey}'.",
            status = "active"
        }, JsonOptions);

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactType = LearningArtifactTypes.LearningSignal,
            NormalizedArtifactType = normalizedType,
            Name = $"Learning signal: {patternKey}",
            Description = "Rollup learning signal from repeated decision evidence.",
            OwnerUserId = ownerUserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = patternKey,
            PayloadJson = signalPayload,
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
