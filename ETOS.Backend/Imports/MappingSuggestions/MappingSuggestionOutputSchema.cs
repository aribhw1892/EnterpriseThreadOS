namespace ETOS.Backend.Imports.MappingSuggestions;

public static class MappingSuggestionOutputSchema
{
    public const string Json = """
        {
          "type": "object",
          "required": ["columnSuggestions", "lifecycleSuggestions"],
          "properties": {
            "columnSuggestions": {
              "type": "array",
              "items": {
                "type": "object",
                "required": [
                  "sourceColumn",
                  "canonicalObjectType",
                  "canonicalAttributeKey",
                  "isIdentityField",
                  "isRequired",
                  "confidence",
                  "rationale"
                ],
                "properties": {
                  "sourceColumn": { "type": "string" },
                  "canonicalObjectType": { "type": "string" },
                  "canonicalAttributeKey": { "type": "string" },
                  "isIdentityField": { "type": "boolean" },
                  "isRequired": { "type": "boolean" },
                  "confidence": { "type": "number" },
                  "rationale": { "type": "string" }
                }
              }
            },
            "lifecycleSuggestions": {
              "type": "array",
              "items": {
                "type": "object",
                "required": [
                  "sourceValue",
                  "canonicalLifecycleKey",
                  "confidence",
                  "rationale"
                ],
                "properties": {
                  "sourceValue": { "type": "string" },
                  "canonicalLifecycleKey": { "type": "string" },
                  "confidence": { "type": "number" },
                  "rationale": { "type": "string" }
                }
              }
            }
          }
        }
        """;
}
