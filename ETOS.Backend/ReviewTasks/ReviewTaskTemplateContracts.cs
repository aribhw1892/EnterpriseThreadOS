namespace ETOS.Backend.ReviewTasks;

public sealed record ReviewTaskTemplatePriorityRuleResponse(
    string SeverityWeight,
    string TrustWeight,
    string ConflictWeight);

public sealed record ReviewTaskTemplateEscalationPathResponse(
    bool Enabled,
    string? EscalationTargetRoleKey,
    string? EscalationPolicyId,
    string? SlaPolicyVersion);

public sealed record ReviewTaskTemplateDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string TemplateKey,
    string ReviewTaskType,
    IReadOnlyCollection<ReviewTaskTemplatePriorityRuleResponse> PriorityRules,
    bool RequiresDataQualityPrerequisite,
    ReviewTaskTemplateEscalationPathResponse EscalationPath,
    IReadOnlyDictionary<string, string> ParticipantRoleDefaults,
    IReadOnlyCollection<string> AllowedOutcomeOptions);

public sealed record ReviewTaskTemplateArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? TemplateKey,
    string? ReviewTaskType,
    DateTimeOffset UpdatedAt);

public sealed record CreateReviewTaskTemplateRequest(
    string Name,
    string? Description,
    string TemplateKey,
    string ReviewTaskType,
    IReadOnlyCollection<ReviewTaskTemplatePriorityRuleRequest>? PriorityRules,
    bool RequiresDataQualityPrerequisite,
    ReviewTaskTemplateEscalationPathRequest? EscalationPath,
    IReadOnlyDictionary<string, string>? ParticipantRoleDefaults,
    IReadOnlyCollection<string>? AllowedOutcomeOptions);

public sealed record CreateReviewTaskTemplateVersionRequest(
    string VersionLabel,
    string? Summary,
    string TemplateKey,
    string ReviewTaskType,
    IReadOnlyCollection<ReviewTaskTemplatePriorityRuleRequest>? PriorityRules,
    bool RequiresDataQualityPrerequisite,
    ReviewTaskTemplateEscalationPathRequest? EscalationPath,
    IReadOnlyDictionary<string, string>? ParticipantRoleDefaults,
    IReadOnlyCollection<string>? AllowedOutcomeOptions);

public sealed record ReviewTaskTemplatePriorityRuleRequest(
    string SeverityWeight,
    string TrustWeight,
    string ConflictWeight);

public sealed record ReviewTaskTemplateEscalationPathRequest(
    bool Enabled,
    string? EscalationTargetRoleKey,
    string? EscalationPolicyId,
    string? SlaPolicyVersion);

public sealed record CreateReviewTaskTemplateResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateReviewTaskTemplateVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkReviewTaskTemplateReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes);

public sealed record PublishReviewTaskTemplateResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
