namespace ETOS.Backend.Agents;

public static class DirectResponseOutputSchema
{
    public const string Json = """
        {
          "type": "object",
          "required": ["message"],
          "properties": {
            "message": { "type": "string" },
            "rationale": { "type": "string" }
          }
        }
        """;
}
