using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.Identity;
using Json.Schema;
using JsonSchemaDocument = Json.Schema.JsonSchema;

namespace ETOS.Backend.Platform.JsonSchema;

public interface IJsonSchemaValidator
{
    void ValidateSchemaDefinition(string schemaJson);

    void ValidateDocumentAgainstSchema(string documentJson, string schemaJson);

    IReadOnlyCollection<string> ValidateSchemaCompatibility(string outputSchemaJson, string referencedSchemaJson);
}

public sealed class JsonSchemaValidatorService : IJsonSchemaValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag
    };

    public void ValidateSchemaDefinition(string schemaJson)
    {
        var schema = ParseSchema(schemaJson, "Schema definition is invalid.");
        _ = schema;
    }

    public void ValidateDocumentAgainstSchema(string documentJson, string schemaJson)
    {
        JsonNode document;
        try
        {
            document = JsonNode.Parse(documentJson) ?? throw new RequestValidationException("Document payload was empty.");
        }
        catch (JsonException exception)
        {
            throw new RequestValidationException($"Document payload is not valid JSON: {exception.Message}");
        }

        var schema = ParseSchema(schemaJson, "Schema definition is invalid.");
        var evaluation = schema.Evaluate(document, EvaluationOptions);
        if (!evaluation.IsValid)
        {
            throw new RequestValidationException("Document payload failed schema validation.");
        }
    }

    public IReadOnlyCollection<string> ValidateSchemaCompatibility(string outputSchemaJson, string referencedSchemaJson)
    {
        var notes = new List<string>();
        JsonObject outputSchema;
        JsonObject referencedSchema;
        try
        {
            outputSchema = JsonNode.Parse(outputSchemaJson) as JsonObject
                ?? throw new RequestValidationException("Output schema is invalid.");
            referencedSchema = JsonNode.Parse(referencedSchemaJson) as JsonObject
                ?? throw new RequestValidationException("Referenced output schema is invalid.");
        }
        catch (JsonException exception)
        {
            notes.Add($"Schema compatibility check failed: {exception.Message}");
            return notes;
        }

        if (referencedSchema["required"] is JsonArray requiredProperties)
        {
            var outputProperties = outputSchema["properties"] as JsonObject ?? new JsonObject();
            foreach (var requiredNode in requiredProperties)
            {
                var propertyName = requiredNode.GetValue<string>();
                if (!outputProperties.ContainsKey(propertyName))
                {
                    notes.Add($"Output schema is missing required property '{propertyName}' from referenced output schema.");
                }
            }
        }

        return notes;
    }

    private static JsonSchemaDocument ParseSchema(string schemaJson, string errorMessage)
    {
        try
        {
            return JsonSchemaDocument.FromText(schemaJson);
        }
        catch (Exception exception)
        {
            throw new RequestValidationException($"{errorMessage} {exception.Message}");
        }
    }
}
