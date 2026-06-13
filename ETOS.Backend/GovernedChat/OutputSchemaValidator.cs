using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.Identity;

namespace ETOS.Backend.GovernedChat;

public interface IOutputSchemaValidator
{
    void Validate(string outputJson, string schemaJson);
}

public sealed class OutputSchemaValidator : IOutputSchemaValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Validate(string outputJson, string schemaJson)
    {
        JsonNode output;
        JsonObject schema;
        try
        {
            output = JsonNode.Parse(outputJson) ?? throw new RequestValidationException("LLM output was empty.");
            schema = JsonNode.Parse(schemaJson) as JsonObject
                ?? throw new RequestValidationException("Output schema artifact payload is invalid.");
        }
        catch (JsonException exception)
        {
            throw new RequestValidationException($"LLM output is not valid JSON: {exception.Message}");
        }

        ValidateNode(output, schema, "$");
    }

    private static void ValidateNode(JsonNode? value, JsonObject schema, string path)
    {
        var type = schema["type"]?.GetValue<string>();
        if (type is null)
        {
            return;
        }

        switch (type)
        {
            case "object":
                ValidateObject(value, schema, path);
                break;
            case "array":
                ValidateArray(value, schema, path);
                break;
            case "string":
                if (value is not JsonValue || value.GetValueKind() != JsonValueKind.String)
                {
                    throw new RequestValidationException($"Expected string at {path}.");
                }

                break;
            case "number":
            case "integer":
                if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<double>(out _))
                {
                    throw new RequestValidationException($"Expected number at {path}.");
                }

                break;
            case "boolean":
                if (value is not JsonValue || (value.GetValueKind() != JsonValueKind.True && value.GetValueKind() != JsonValueKind.False))
                {
                    throw new RequestValidationException($"Expected boolean at {path}.");
                }

                break;
            default:
                break;
        }
    }

    private static void ValidateObject(JsonNode? value, JsonObject schema, string path)
    {
        if (value is not JsonObject obj)
        {
            throw new RequestValidationException($"Expected object at {path}.");
        }

        if (schema["required"] is JsonArray required)
        {
            foreach (var requiredNode in required)
            {
                var propertyName = requiredNode?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                if (!obj.ContainsKey(propertyName))
                {
                    throw new RequestValidationException($"Missing required property '{propertyName}' at {path}.");
                }
            }
        }

        if (schema["properties"] is not JsonObject properties)
        {
            return;
        }

        foreach (var property in properties)
        {
            if (property.Value is not JsonObject propertySchema || !obj.TryGetPropertyValue(property.Key, out var propertyValue))
            {
                continue;
            }

            ValidateNode(propertyValue, propertySchema, $"{path}.{property.Key}");
        }
    }

    private static void ValidateArray(JsonNode? value, JsonObject schema, string path)
    {
        if (value is not JsonArray array)
        {
            throw new RequestValidationException($"Expected array at {path}.");
        }

        if (schema["items"] is not JsonObject itemSchema)
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            ValidateNode(array[index], itemSchema, $"{path}[{index}]");
        }
    }
}
