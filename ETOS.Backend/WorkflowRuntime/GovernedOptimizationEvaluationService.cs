using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class GovernedOptimizationEvaluationService(EnterpriseThreadDbContext dbContext)
    : IGovernedOptimizationEvaluationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GovernedOptimizationEvaluationResult> EvaluateAsync(
        Guid optimizationModelVersionId,
        string contextJson,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == optimizationModelVersionId, cancellationToken)
            ?? throw new RequestValidationException("Optimization model version was not found.");

        var document = OptimizationModelDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var objectiveMetadata = document.ObjectiveMetadata ?? new Dictionary<string, string>();
        var solverConfiguration = document.SolverConfiguration ?? new Dictionary<string, string>();

        JsonObject contextNode;
        try
        {
            contextNode = JsonNode.Parse(string.IsNullOrWhiteSpace(contextJson) ? "{}" : contextJson) as JsonObject
                ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new GovernedOptimizationEvaluationResult(
                false,
                "{}",
                "Workflow context is not valid JSON for optimization evaluation.");
        }

        var scoreField = ResolveField(solverConfiguration, "scoreField", "targetField", "objectiveField");
        var direction = ResolveField(solverConfiguration, "direction", "objectiveDirection");
        if (string.IsNullOrWhiteSpace(direction))
        {
            direction = ResolveField(objectiveMetadata, "direction", "objectiveDirection");
        }

        direction = string.IsNullOrWhiteSpace(direction) ? "minimize" : direction.Trim();

        var candidateScores = new List<OptimizationCandidateScore>();
        if (contextNode.TryGetPropertyValue("candidates", out var candidatesNode) && candidatesNode is JsonArray candidatesArray)
        {
            foreach (var candidateNode in candidatesArray)
            {
                if (candidateNode is not JsonObject candidateObject)
                {
                    continue;
                }

                var score = ComputeCandidateScore(candidateObject, objectiveMetadata, solverConfiguration, scoreField, direction);
                candidateScores.Add(score);
            }
        }
        else
        {
            candidateScores.Add(ComputeCandidateScore(contextNode, objectiveMetadata, solverConfiguration, scoreField, direction));
        }

        candidateScores = direction.Equals("maximize", StringComparison.OrdinalIgnoreCase)
            ? candidateScores.OrderByDescending(item => item.Score).ToList()
            : candidateScores.OrderBy(item => item.Score).ToList();

        var best = candidateScores.FirstOrDefault();
        var result = new JsonObject
        {
            ["optimizationKey"] = document.OptimizationKey,
            ["objectiveCategory"] = document.ObjectiveCategory,
            ["direction"] = direction,
            ["bestScore"] = best?.Score,
            ["bestCandidateKey"] = best?.CandidateKey,
            ["rankedCandidates"] = JsonSerializer.SerializeToNode(candidateScores.Select(item => new
            {
                item.CandidateKey,
                item.Score,
                item.MatchedMetadataKeys
            }).ToList())
        };

        foreach (var pair in objectiveMetadata)
        {
            if (contextNode.TryGetPropertyValue(pair.Key, out var value))
            {
                result[pair.Key] = value?.DeepClone();
            }
        }

        var mergedContext = JsonNode.Parse(contextJson) as JsonObject ?? new JsonObject();
        mergedContext["optimizationEvaluation"] = result;

        return new GovernedOptimizationEvaluationResult(
            true,
            mergedContext.ToJsonString(JsonOptions),
            null);
    }

    private static OptimizationCandidateScore ComputeCandidateScore(
        JsonObject candidate,
        IReadOnlyDictionary<string, string> objectiveMetadata,
        IReadOnlyDictionary<string, string> solverConfiguration,
        string? scoreField,
        string direction)
    {
        var matchedKeys = 0;
        decimal aggregateScore = 0m;
        var weightSum = 0m;

        foreach (var metadataEntry in objectiveMetadata)
        {
            if (!candidate.TryGetPropertyValue(metadataEntry.Key, out var actualNode))
            {
                continue;
            }

            matchedKeys++;
            var weight = ResolveWeight(solverConfiguration, metadataEntry.Key);
            var actual = ParseDecimal(actualNode);
            var expected = ParseDecimal(metadataEntry.Value);
            var delta = Math.Abs(actual - expected);
            var contribution = direction.Equals("maximize", StringComparison.OrdinalIgnoreCase)
                ? actual * weight
                : delta * weight;
            aggregateScore += contribution;
            weightSum += weight;
        }

        if (!string.IsNullOrWhiteSpace(scoreField) && candidate.TryGetPropertyValue(scoreField, out var scoreNode))
        {
            aggregateScore = ParseDecimal(scoreNode);
            weightSum = 1m;
        }
        else if (weightSum > 0m)
        {
            aggregateScore /= weightSum;
        }

        var candidateKey = candidate.TryGetPropertyValue("key", out var keyNode)
            ? keyNode?.ToString()
            : candidate.TryGetPropertyValue("id", out var idNode)
                ? idNode?.ToString()
                : null;

        return new OptimizationCandidateScore(candidateKey, aggregateScore, matchedKeys);
    }

    private static decimal ResolveWeight(IReadOnlyDictionary<string, string> solverConfiguration, string metadataKey)
    {
        var weightKey = $"weight:{metadataKey}";
        if (solverConfiguration.TryGetValue(weightKey, out var explicitWeight))
        {
            return ParseDecimal(explicitWeight);
        }

        if (solverConfiguration.TryGetValue("defaultWeight", out var defaultWeight))
        {
            return ParseDecimal(defaultWeight);
        }

        return 1m;
    }

    private static decimal ParseDecimal(JsonNode? node)
    {
        if (node is null)
        {
            return 0m;
        }

        if (decimal.TryParse(node.ToString(), out var value))
        {
            return value;
        }

        return 0m;
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, out var parsed) ? parsed : 0m;

    private static string? ResolveField(IReadOnlyDictionary<string, string> source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (source.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private sealed record OptimizationCandidateScore(string? CandidateKey, decimal Score, int MatchedMetadataKeys);
}
