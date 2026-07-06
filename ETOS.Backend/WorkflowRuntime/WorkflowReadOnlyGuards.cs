using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.WorkflowRuntime;

internal static class WorkflowReadOnlyGuards
{
    public static void GuardAgainstDecisionCreation(string? outputSchemaJson, string structuredOutputJson)
    {
        if (OutputSchemaCreatesDecision(outputSchemaJson))
        {
            throw new RequestValidationException("Workflow output must not create decision artifacts.");
        }

        try
        {
            using var output = JsonDocument.Parse(structuredOutputJson);
            foreach (var property in output.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    throw new RequestValidationException("Workflow structured output must not create decision artifacts.");
                }
            }
        }
        catch (JsonException)
        {
            throw new RequestValidationException("Workflow structured output is not valid JSON.");
        }
    }

    public static void GuardStructuredOutputAgainstDecisionCreation(string structuredOutputJson)
    {
        try
        {
            using var output = JsonDocument.Parse(structuredOutputJson);
            foreach (var property in output.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    throw new RequestValidationException("Workflow structured output must not create decision artifacts.");
                }
            }
        }
        catch (JsonException)
        {
            throw new RequestValidationException("Workflow structured output is not valid JSON.");
        }
    }

    private static bool OutputSchemaCreatesDecision(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
