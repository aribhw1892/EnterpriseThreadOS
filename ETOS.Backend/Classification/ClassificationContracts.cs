namespace ETOS.Backend.Classification;

public static class ClassificationPermissions
{
    public const string Read = "classification.read";
    public const string Manage = "classification.manage";
    public const string Publish = "classification.publish";
    public const string Evaluate = "policy.evaluate";
    public const string Admin = "policy.admin";
}

public sealed record CreateClassificationSchemeRequest(
    string Key,
    string Name,
    string? Description);

public sealed record CreateClassificationSchemeVersionRequest(
    string VersionLabel,
    string? Summary,
    string? LevelsJson);

public sealed record PublishClassificationSchemeVersionRequest(string? Summary);

public sealed record CreatePolicyVersionRequest(
    string PolicyKey,
    string Name,
    string VersionLabel,
    string? Summary,
    Guid ClassificationSchemeVersionId);

public sealed record CreateRestrictedContextRuleRequest(
    string ClassificationKey,
    string? AttributeKey,
    string? DocumentType,
    string? RequiredPermissionKey,
    string? AllowedRoleName,
    bool RequiresGrant,
    PolicyRuleEffect Effect,
    string SafeSummary);

public sealed record PublishPolicyVersionRequest(string? Summary);

public sealed record ClassificationSchemeResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string Name,
    string? Description,
    ClassificationSchemeVersionResponse? LatestVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ClassificationSchemeVersionResponse(
    Guid Id,
    Guid TenantId,
    Guid SchemeId,
    string SchemeKey,
    string VersionLabel,
    string? Summary,
    string? LevelsJson,
    PolicyPublicationState State,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record PolicyVersionResponse(
    Guid Id,
    Guid TenantId,
    string PolicyKey,
    string Name,
    string VersionLabel,
    string? Summary,
    Guid ClassificationSchemeVersionId,
    string ClassificationSchemeVersionLabel,
    PolicyPublicationState State,
    int RestrictedRuleCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record RestrictedContextRuleResponse(
    Guid Id,
    Guid TenantId,
    Guid PolicyVersionId,
    string PolicyKey,
    string ClassificationKey,
    string? AttributeKey,
    string? DocumentType,
    string? RequiredPermissionKey,
    string? AllowedRoleName,
    bool RequiresGrant,
    PolicyRuleEffect Effect,
    string SafeSummary,
    DateTimeOffset CreatedAt);

public sealed record PolicyEvaluationContextItem(
    string ContextId,
    string ContextType,
    string ClassificationKey,
    string? AttributeKey,
    string? DocumentId,
    string SafeSummary);

public sealed record EvaluatePolicyRequest(
    string Action,
    string? PolicyKey,
    IReadOnlyCollection<PolicyEvaluationContextItem> Items);

public sealed record PolicyAllowedContextResponse(
    string ContextId,
    string ContextType,
    string SafeSummary);

public sealed record PolicyDeniedSummaryResponse(
    string ContextId,
    string ContextType,
    string SafeSummary,
    string Reason,
    PolicyRuleEffect Effect);

public sealed record PolicySensitiveDeniedReferenceResponse(
    string ContextId,
    string ContextType,
    string? DocumentId,
    string ClassificationKey,
    string? AttributeKey,
    string Reason);

public sealed record PolicyEvaluationResponse(
    Guid EvaluationId,
    Guid? PolicyVersionId,
    string? PolicyKey,
    string? PolicyVersionLabel,
    IReadOnlyCollection<PolicyAllowedContextResponse> AllowedContext,
    IReadOnlyCollection<PolicyDeniedSummaryResponse> DeniedSummaries,
    IReadOnlyCollection<PolicySensitiveDeniedReferenceResponse> SensitiveDeniedReferences);

public sealed record PolicyImpactResponse(
    Guid PolicyVersionId,
    string PolicyKey,
    string VersionLabel,
    int RestrictedRuleCount,
    int AffectedArtifactCount,
    IReadOnlyCollection<PolicyAffectedArtifactResponse> AffectedArtifacts);

public sealed record PolicyAffectedArtifactResponse(
    Guid ArtifactId,
    string ArtifactName,
    string ArtifactType,
    Guid? LatestVersionId,
    string? LatestVersionLabel,
    string PolicyRiskStatus);
