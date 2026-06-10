using System.Text.Json.Serialization;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Ontology;

public interface ITenantVersion : ITenantScoped
{
    Guid Id { get; }
    string NormalizedKey { get; }
    string NormalizedVersionLabel { get; }
    OntologyPublicationState State { get; }
}

public interface IMutablePublishedVersion : ITenantVersion
{
    string VersionLabel { get; }
    string? Summary { get; set; }
    new OntologyPublicationState State { get; set; }
    Guid? PublishedByUserId { get; set; }
    DateTimeOffset? PublishedAt { get; set; }
}

public sealed class OntologyVersion : IMutablePublishedVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public string? Summary { get; set; }
    public OntologyPublicationState State { get; set; } = OntologyPublicationState.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<OntologyObjectTypeDefinition> ObjectTypes { get; set; } = [];
    public List<SemanticRelationshipDefinition> RelationshipTypes { get; set; } = [];
    public List<BomRelationshipDefinition> BomRelationships { get; set; } = [];
}

public sealed class OntologyObjectTypeDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OntologyVersionId { get; set; }
    public OntologyVersion? OntologyVersion { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public string? VersionIdentityFieldsJson { get; set; }
    public required string SafeSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SemanticRelationshipDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OntologyVersionId { get; set; }
    public OntologyVersion? OntologyVersion { get; set; }
    public required string RelationshipType { get; set; }
    public required string NormalizedRelationshipType { get; set; }
    public required string FromObjectType { get; set; }
    public required string NormalizedFromObjectType { get; set; }
    public required string ToObjectType { get; set; }
    public required string NormalizedToObjectType { get; set; }
    public string? Description { get; set; }
    public bool IsVersionRelationship { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BomRelationshipDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OntologyVersionId { get; set; }
    public OntologyVersion? OntologyVersion { get; set; }
    public required string RelationshipType { get; set; }
    public required string NormalizedRelationshipType { get; set; }
    public required string ParentObjectType { get; set; }
    public required string NormalizedParentObjectType { get; set; }
    public required string ChildObjectType { get; set; }
    public required string NormalizedChildObjectType { get; set; }
    public string? QuantityAttributeKey { get; set; }
    public string? UnitAttributeKey { get; set; }
    public string? FindNumberAttributeKey { get; set; }
    public string? ReferenceDesignatorAttributeKey { get; set; }
    public string? LifecycleConstraintJson { get; set; }
    public bool RequiresApproval { get; set; }
    public string? AuditReferenceAttributeKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SemanticLayerVersion : IMutablePublishedVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public string? Summary { get; set; }
    public Guid OntologyVersionId { get; set; }
    public OntologyVersion? OntologyVersion { get; set; }
    public string? GraphNodeTypeMappingsJson { get; set; }
    public string? GraphRelationshipTypeMappingsJson { get; set; }
    public OntologyPublicationState State { get; set; } = OntologyPublicationState.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class LifecycleVocabularyVersion : IMutablePublishedVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public string? Summary { get; set; }
    public OntologyPublicationState State { get; set; } = OntologyPublicationState.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<LifecycleStateDefinition> States { get; set; } = [];
    public List<LifecycleTransitionDefinition> Transitions { get; set; } = [];
}

public sealed class LifecycleStateDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LifecycleVocabularyVersionId { get; set; }
    public LifecycleVocabularyVersion? LifecycleVocabularyVersion { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string DisplayName { get; set; }
    public string? NormalizedCategory { get; set; }
    public int SortOrder { get; set; }
    public bool IsTerminal { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LifecycleTransitionDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LifecycleVocabularyVersionId { get; set; }
    public LifecycleVocabularyVersion? LifecycleVocabularyVersion { get; set; }
    public required string FromStateKey { get; set; }
    public required string NormalizedFromStateKey { get; set; }
    public required string ToStateKey { get; set; }
    public required string NormalizedToStateKey { get; set; }
    public bool RequiresApproval { get; set; }
    public string? SafeSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AttributeSchemaVersion : IMutablePublishedVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public string? Summary { get; set; }
    public Guid OntologyVersionId { get; set; }
    public OntologyVersion? OntologyVersion { get; set; }
    public OntologyPublicationState State { get; set; } = OntologyPublicationState.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<AttributeDefinition> Attributes { get; set; } = [];
}

public sealed class AttributeDefinition : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AttributeSchemaVersionId { get; set; }
    public AttributeSchemaVersion? AttributeSchemaVersion { get; set; }
    public required string AttributeKey { get; set; }
    public required string NormalizedAttributeKey { get; set; }
    public required string AppliesToObjectType { get; set; }
    public required string NormalizedAppliesToObjectType { get; set; }
    public AttributeValueType ValueType { get; set; } = AttributeValueType.Text;
    public bool IsRequired { get; set; }
    public string? ValidationRulesJson { get; set; }
    public AttributeVisibility Visibility { get; set; } = AttributeVisibility.Internal;
    public string? RequiredPermissionKey { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsAiFacing { get; set; }
    public string? ClassificationKey { get; set; }
    public string? DisplayName { get; set; }
    public required string SafeSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ModelPackageVersion : IMutablePublishedVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string NormalizedKey { get; set; }
    public required string Name { get; set; }
    public required string VersionLabel { get; set; }
    public required string NormalizedVersionLabel { get; set; }
    public string? Summary { get; set; }
    public Guid OntologyVersionId { get; set; }
    public OntologyVersion? OntologyVersion { get; set; }
    public Guid SemanticLayerVersionId { get; set; }
    public SemanticLayerVersion? SemanticLayerVersion { get; set; }
    public Guid LifecycleVocabularyVersionId { get; set; }
    public LifecycleVocabularyVersion? LifecycleVocabularyVersion { get; set; }
    public Guid AttributeSchemaVersionId { get; set; }
    public AttributeSchemaVersion? AttributeSchemaVersion { get; set; }
    public Guid? ArtifactId { get; set; }
    public Guid? ArtifactVersionId { get; set; }
    public OntologyPublicationState State { get; set; } = OntologyPublicationState.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OntologyPublicationState
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttributeValueType
{
    Text = 0,
    Number = 1,
    Integer = 2,
    Boolean = 3,
    Date = 4,
    DateTime = 5,
    Json = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttributeVisibility
{
    Public = 0,
    Internal = 1,
    Restricted = 2
}
