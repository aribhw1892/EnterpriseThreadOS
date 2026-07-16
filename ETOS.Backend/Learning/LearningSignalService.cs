using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Learning;

public interface ILearningSignalService
{
    Task<IReadOnlyCollection<LearningSignalSummaryResponse>> ListAsync(
        string? status,
        string? patternKey,
        CancellationToken cancellationToken);

    Task<LearningSignalDetailResponse> GetAsync(Guid artifactId, CancellationToken cancellationToken);
}

public sealed class LearningSignalService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService) : ILearningSignalService
{
    public async Task<IReadOnlyCollection<LearningSignalSummaryResponse>> ListAsync(
        string? status,
        string? patternKey,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("learning-signals.list", cancellationToken);
        var normalizedType = LearningArtifactTypes.LearningSignal.ToUpperInvariant();

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId && artifact.NormalizedArtifactType == normalizedType)
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var responses = new List<LearningSignalSummaryResponse>();
        foreach (var artifact in artifacts)
        {
            var version = await LoadLatestVersionAsync(artifact.Id, cancellationToken);
            if (version is null)
            {
                continue;
            }

            var summary = ToSummary(artifact, version);
            if (!MatchesFilter(summary, status, patternKey))
            {
                continue;
            }

            responses.Add(summary);
        }

        return responses;
    }

    public async Task<LearningSignalDetailResponse> GetAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var context = await RequireReadAsync("learning-signals.get", cancellationToken);
        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == artifactId
                    && item.TenantId == context.TenantId
                    && item.NormalizedArtifactType == LearningArtifactTypes.LearningSignal.ToUpperInvariant(),
                cancellationToken)
            ?? throw new RequestValidationException("Learning signal artifact was not found.");

        var version = await LoadLatestVersionAsync(artifact.Id, cancellationToken)
            ?? throw new RequestValidationException("Learning signal version was not found.");

        var payload = ParsePayload(version.PayloadJson);
        var evidence = await dbContext.DecisionLearningEvidence
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.PatternKey == payload.PatternKey)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .Select(item => new LearningEvidenceSummaryResponse(
                item.Id,
                item.DecisionArtifactId,
                item.PatternKey,
                item.SourceType,
                item.OutcomeKey,
                item.EvidenceSummary,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return new LearningSignalDetailResponse(
            artifact.Id,
            version.Id,
            artifact.Name,
            payload.PatternKey,
            payload.OccurrenceCount,
            payload.Summary,
            payload.Status,
            payload.SourceDecisionIds,
            evidence,
            artifact.CreatedAt,
            artifact.UpdatedAt);
    }

    private async Task<ArtifactVersion?> LoadLatestVersionAsync(Guid artifactId, CancellationToken cancellationToken)
        => await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.ArtifactId == artifactId)
            .OrderByDescending(version => version.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ActiveTenantContext> RequireReadAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, LearningPermissions.Read, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, LearningPermissions.Admin, cancellationToken)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            throw new TenantAccessDeniedException("Learning signal read permission is required.");
        }

        return context;
    }

    private static LearningSignalSummaryResponse ToSummary(Artifact artifact, ArtifactVersion version)
    {
        var payload = ParsePayload(version.PayloadJson);
        return new LearningSignalSummaryResponse(
            artifact.Id,
            version.Id,
            artifact.Name,
            payload.PatternKey,
            payload.OccurrenceCount,
            payload.Summary,
            payload.Status,
            payload.SourceDecisionIds,
            artifact.UpdatedAt);
    }

    private static bool MatchesFilter(LearningSignalSummaryResponse summary, string? status, string? patternKey)
    {
        if (!string.IsNullOrWhiteSpace(status)
            && !summary.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(patternKey)
            && !summary.PatternKey.Contains(patternKey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static LearningSignalPayload ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new LearningSignalPayload(string.Empty, 0, string.Empty, "unknown", []);
        }

        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var patternKey = root.TryGetProperty("patternKey", out var patternElement)
            ? patternElement.GetString() ?? string.Empty
            : string.Empty;
        var occurrenceCount = root.TryGetProperty("occurrenceCount", out var countElement)
            && countElement.TryGetInt32(out var count)
            ? count
            : 0;
        var summary = root.TryGetProperty("summary", out var summaryElement)
            ? summaryElement.GetString() ?? string.Empty
            : string.Empty;
        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "unknown"
            : "unknown";

        var sourceDecisionIds = new List<Guid>();
        if (root.TryGetProperty("sourceDecisionIds", out var sourceElement)
            && sourceElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sourceElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && Guid.TryParse(item.GetString(), out var parsedFromString))
                {
                    sourceDecisionIds.Add(parsedFromString);
                }
                else if (item.TryGetGuid(out var parsedGuid))
                {
                    sourceDecisionIds.Add(parsedGuid);
                }
            }
        }

        return new LearningSignalPayload(patternKey, occurrenceCount, summary, status, sourceDecisionIds);
    }

    private sealed record LearningSignalPayload(
        string PatternKey,
        int OccurrenceCount,
        string Summary,
        string Status,
        IReadOnlyCollection<Guid> SourceDecisionIds);
}
