using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ReviewTasks;

public static class ReviewTaskTemplatePayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ReviewTaskTemplateDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new ReviewTaskTemplateDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.TemplateKey!.Trim(),
            document.ReviewTaskType!.Trim(),
            document.PriorityRules?.Select(item => new ReviewTaskTemplatePriorityRuleResponse(
                item.SeverityWeight,
                item.TrustWeight,
                item.ConflictWeight)).ToList() ?? [],
            document.RequiresDataQualityPrerequisite,
            new ReviewTaskTemplateEscalationPathResponse(
                document.EscalationPath?.Enabled ?? false,
                document.EscalationPath?.EscalationTargetRoleKey,
                document.EscalationPath?.EscalationPolicyId,
                document.EscalationPath?.SlaPolicyVersion),
            document.ParticipantRoleDefaults ?? new Dictionary<string, string>(),
            document.AllowedOutcomeOptions ?? []);
    }

    public static string Serialize(ReviewTaskTemplatePayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static ReviewTaskTemplatePayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<ReviewTaskTemplatePayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Review task template payload is invalid.");
        return document;
    }

    public static ReviewTaskTemplatePayloadDocument Create(
        string templateKey,
        string reviewTaskType,
        IReadOnlyCollection<ReviewTaskTemplatePriorityRuleDocument>? priorityRules,
        bool requiresDataQualityPrerequisite,
        ReviewTaskTemplateEscalationPathDocument? escalationPath,
        IReadOnlyDictionary<string, string>? participantRoleDefaults,
        IReadOnlyCollection<string>? allowedOutcomeOptions)
        => Normalize(new ReviewTaskTemplatePayloadDocument
        {
            TemplateKey = templateKey.Trim(),
            ReviewTaskType = reviewTaskType.Trim(),
            PriorityRules = priorityRules?.ToList() ?? DefaultPriorityRules(),
            RequiresDataQualityPrerequisite = requiresDataQualityPrerequisite,
            EscalationPath = escalationPath ?? new ReviewTaskTemplateEscalationPathDocument { Enabled = false },
            ParticipantRoleDefaults = participantRoleDefaults?.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            AllowedOutcomeOptions = allowedOutcomeOptions?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(ReviewTaskTemplatePayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.TemplateKey))
        {
            throw new RequestValidationException("templateKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ReviewTaskType))
        {
            throw new RequestValidationException("reviewTaskType is required.");
        }

        if (document.EscalationPath?.Enabled == true)
        {
            ValidateEscalationPath(document.EscalationPath);
        }
    }

    public static void ValidateEscalationPath(ReviewTaskTemplateEscalationPathDocument escalationPath)
    {
        if (!escalationPath.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(escalationPath.EscalationTargetRoleKey))
        {
            throw new RequestValidationException("escalationTargetRoleKey is required when escalationPath.enabled is true.");
        }
    }

    private static List<ReviewTaskTemplatePriorityRuleDocument> DefaultPriorityRules()
        =>
        [
            new ReviewTaskTemplatePriorityRuleDocument { SeverityWeight = "critical=4,high=3,medium=2,low=1", TrustWeight = "conflicted=3,provisional=2,trusted=1", ConflictWeight = "blocked=3,partial=2,none=1" }
        ];

    private static ReviewTaskTemplatePayloadDocument Normalize(ReviewTaskTemplatePayloadDocument document)
    {
        document.TemplateKey = document.TemplateKey?.Trim() ?? string.Empty;
        document.ReviewTaskType = document.ReviewTaskType?.Trim() ?? string.Empty;
        document.PriorityRules ??= DefaultPriorityRules();
        document.EscalationPath ??= new ReviewTaskTemplateEscalationPathDocument { Enabled = false };
        document.ParticipantRoleDefaults ??= new Dictionary<string, string>();
        document.AllowedOutcomeOptions ??= [];
        return document;
    }

    public sealed class ReviewTaskTemplatePayloadDocument
    {
        public string? TemplateKey { get; set; }
        public string? ReviewTaskType { get; set; }
        public List<ReviewTaskTemplatePriorityRuleDocument>? PriorityRules { get; set; }
        public bool RequiresDataQualityPrerequisite { get; set; }
        public ReviewTaskTemplateEscalationPathDocument? EscalationPath { get; set; }
        public Dictionary<string, string>? ParticipantRoleDefaults { get; set; }
        public List<string>? AllowedOutcomeOptions { get; set; }
    }

    public sealed class ReviewTaskTemplatePriorityRuleDocument
    {
        public string SeverityWeight { get; set; } = "critical=4,high=3,medium=2,low=1";
        public string TrustWeight { get; set; } = "conflicted=3,provisional=2,trusted=1";
        public string ConflictWeight { get; set; } = "blocked=3,partial=2,none=1";
    }

    public sealed class ReviewTaskTemplateEscalationPathDocument
    {
        public bool Enabled { get; set; }
        public string? EscalationTargetRoleKey { get; set; }
        public string? EscalationPolicyId { get; set; }
        public string? SlaPolicyVersion { get; set; }
    }
}
