namespace ETOS.Backend.AgentTypes;

using ETOS.Backend.Identity;

public static class AgentTypeDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        AgentTypeDefinitionPayloadParser.AgentTypeDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        try
        {
            AgentTypeDefinitionPayloadParser.ValidateCore(document);
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
        }

        return notes;
    }
}
