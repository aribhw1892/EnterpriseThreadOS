using System.Text.Json;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class BusinessPolicyWorkflowEvaluator(EnterpriseThreadDbContext dbContext) : IBusinessPolicyWorkflowEvaluator
{
    public async Task<BusinessPolicyWorkflowEvaluationResult> EvaluateAsync(
        Guid businessPolicyDefinitionVersionId,
        string contextJson,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == businessPolicyDefinitionVersionId, cancellationToken)
            ?? throw new RequestValidationException("Business policy definition version was not found.");

        var document = BusinessPolicyDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var constraintRules = document.ConstraintRules ?? new Dictionary<string, string>();

        JsonDocument contextDocument;
        try
        {
            contextDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(contextJson) ? "{}" : contextJson);
        }
        catch (JsonException)
        {
            return new BusinessPolicyWorkflowEvaluationResult(
                false,
                null,
                "Workflow context is not valid JSON for business policy evaluation.");
        }

        using (contextDocument)
        {
            if (contextDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new BusinessPolicyWorkflowEvaluationResult(
                    false,
                    null,
                    "Workflow context must be a JSON object for business policy evaluation.");
            }

            foreach (var rule in constraintRules)
            {
                if (!contextDocument.RootElement.TryGetProperty(rule.Key, out var contextValue))
                {
                    continue;
                }

                var actual = ContextValueToString(contextValue);
                if (!string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return new BusinessPolicyWorkflowEvaluationResult(
                        false,
                        rule.Key,
                        $"Business policy rule '{rule.Key}' expected '{rule.Value}' but context has '{actual}'.");
                }
            }
        }

        return new BusinessPolicyWorkflowEvaluationResult(true, null, null);
    }

    private static string ContextValueToString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
}
