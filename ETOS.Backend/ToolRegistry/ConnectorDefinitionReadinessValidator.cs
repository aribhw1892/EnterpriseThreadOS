using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class ConnectorDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        ConnectorDefinitionPayloadParser.ConnectorDefinitionPayloadDocument document)
    {
        var notes = new List<string>();
        try
        {
            ConnectorDefinitionPayloadParser.ValidateCore(document);
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
        }

        return notes;
    }
}
