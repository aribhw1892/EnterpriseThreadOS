using ETOS.Backend.Identity;

namespace ETOS.Backend.ReviewTasks;

public static class ReviewTaskTemplateReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        ReviewTaskTemplatePayloadParser.ReviewTaskTemplatePayloadDocument document)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(document.TemplateKey))
        {
            notes.Add("templateKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ReviewTaskType))
        {
            notes.Add("reviewTaskType is required.");
        }

        if (document.EscalationPath?.Enabled == true)
        {
            try
            {
                ReviewTaskTemplatePayloadParser.ValidateEscalationPath(document.EscalationPath);
            }
            catch (RequestValidationException exception)
            {
                notes.Add(exception.Message);
            }
        }

        return notes;
    }
}
