using System.Text.Json.Serialization;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Classification;

public sealed class ClassificationScheme : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Key { get; set; }

    public required string NormalizedKey { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ClassificationSchemeVersion> Versions { get; set; } = [];
}

public sealed class ClassificationSchemeVersion : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SchemeId { get; set; }

    public ClassificationScheme? Scheme { get; set; }

    public required string VersionLabel { get; set; }

    public required string NormalizedVersionLabel { get; set; }

    public string? Summary { get; set; }

    public string? LevelsJson { get; set; }

    public PolicyPublicationState State { get; set; } = PolicyPublicationState.Draft;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? PublishedByUserId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class PolicyVersion : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string PolicyKey { get; set; }

    public required string NormalizedPolicyKey { get; set; }

    public required string Name { get; set; }

    public required string VersionLabel { get; set; }

    public required string NormalizedVersionLabel { get; set; }

    public string? Summary { get; set; }

    public Guid ClassificationSchemeVersionId { get; set; }

    public ClassificationSchemeVersion? ClassificationSchemeVersion { get; set; }

    public PolicyPublicationState State { get; set; } = PolicyPublicationState.Draft;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? PublishedByUserId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public List<RestrictedContextRule> RestrictedRules { get; set; } = [];
}

public sealed class RestrictedContextRule : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PolicyVersionId { get; set; }

    public PolicyVersion? PolicyVersion { get; set; }

    public required string ClassificationKey { get; set; }

    public required string NormalizedClassificationKey { get; set; }

    public string? AttributeKey { get; set; }

    public string? NormalizedAttributeKey { get; set; }

    public string? DocumentType { get; set; }

    public string? NormalizedDocumentType { get; set; }

    public string? RequiredPermissionKey { get; set; }

    public string? NormalizedRequiredPermissionKey { get; set; }

    public string? AllowedRoleName { get; set; }

    public string? NormalizedAllowedRoleName { get; set; }

    public bool RequiresGrant { get; set; }

    public PolicyRuleEffect Effect { get; set; } = PolicyRuleEffect.Deny;

    public required string SafeSummary { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PolicyEvaluationRecord : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid? PolicyVersionId { get; set; }

    public required string Action { get; set; }

    public int AllowedCount { get; set; }

    public int DeniedCount { get; set; }

    public required string SafeSummary { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolicyPublicationState
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolicyRuleEffect
{
    Deny = 0,
    RequiresApproval = 1
}
