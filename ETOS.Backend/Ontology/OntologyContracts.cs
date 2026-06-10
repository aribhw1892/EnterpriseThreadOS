namespace ETOS.Backend.Ontology;

public static class OntologyPermissions
{
    public const string Read = "ontology.read";
    public const string Manage = "ontology.manage";
    public const string Publish = "ontology.publish";
    public const string Admin = "ontology.admin";
}

public sealed record CreateOntologyVersionRequest(
    string Key,
    string VersionLabel,
    string? Summary,
    IReadOnlyCollection<CreateObjectTypeDefinitionRequest> ObjectTypes,
    IReadOnlyCollection<CreateSemanticRelationshipDefinitionRequest> RelationshipTypes,
    IReadOnlyCollection<CreateBomRelationshipDefinitionRequest> BomRelationships);

public sealed record CreateObjectTypeDefinitionRequest(
    string Key,
    string DisplayName,
    string? Description,
    string? VersionIdentityFieldsJson,
    string SafeSummary);

public sealed record CreateSemanticRelationshipDefinitionRequest(
    string RelationshipType,
    string FromObjectType,
    string ToObjectType,
    string? Description,
    bool IsVersionRelationship);

public sealed record CreateBomRelationshipDefinitionRequest(
    string RelationshipType,
    string ParentObjectType,
    string ChildObjectType,
    string? QuantityAttributeKey,
    string? UnitAttributeKey,
    string? FindNumberAttributeKey,
    string? ReferenceDesignatorAttributeKey,
    string? LifecycleConstraintJson,
    bool RequiresApproval,
    string? AuditReferenceAttributeKey);

public sealed record CreateSemanticLayerVersionRequest(
    string Key,
    string VersionLabel,
    string? Summary,
    Guid OntologyVersionId,
    string? GraphNodeTypeMappingsJson,
    string? GraphRelationshipTypeMappingsJson);

public sealed record CreateLifecycleVocabularyVersionRequest(
    string Key,
    string VersionLabel,
    string? Summary,
    IReadOnlyCollection<CreateLifecycleStateDefinitionRequest> States,
    IReadOnlyCollection<CreateLifecycleTransitionDefinitionRequest> Transitions);

public sealed record CreateLifecycleStateDefinitionRequest(
    string Key,
    string DisplayName,
    string? Category,
    int SortOrder,
    bool IsTerminal);

public sealed record CreateLifecycleTransitionDefinitionRequest(
    string FromStateKey,
    string ToStateKey,
    bool RequiresApproval,
    string? SafeSummary);

public sealed record CreateAttributeSchemaVersionRequest(
    string Key,
    string VersionLabel,
    string? Summary,
    Guid OntologyVersionId,
    IReadOnlyCollection<CreateAttributeDefinitionRequest> Attributes);

public sealed record CreateAttributeDefinitionRequest(
    string AttributeKey,
    string AppliesToObjectType,
    AttributeValueType ValueType,
    bool IsRequired,
    string? ValidationRulesJson,
    AttributeVisibility Visibility,
    string? RequiredPermissionKey,
    bool IsSearchable,
    bool IsAiFacing,
    string? ClassificationKey,
    string? DisplayName,
    string SafeSummary);

public sealed record CreateModelPackageVersionRequest(
    string Key,
    string Name,
    string VersionLabel,
    string? Summary,
    Guid OntologyVersionId,
    Guid SemanticLayerVersionId,
    Guid LifecycleVocabularyVersionId,
    Guid AttributeSchemaVersionId);

public sealed record PublishOntologyVersionRequest(string? Summary);

public sealed record ModelPackagePreviewRequest(
    Guid OntologyVersionId,
    Guid SemanticLayerVersionId,
    Guid LifecycleVocabularyVersionId,
    Guid AttributeSchemaVersionId);

public sealed record ObjectTypeDefinitionResponse(
    Guid Id,
    string Key,
    string DisplayName,
    string? Description,
    string? VersionIdentityFieldsJson,
    string SafeSummary);

public sealed record SemanticRelationshipDefinitionResponse(
    Guid Id,
    string RelationshipType,
    string FromObjectType,
    string ToObjectType,
    string? Description,
    bool IsVersionRelationship);

public sealed record BomRelationshipDefinitionResponse(
    Guid Id,
    string RelationshipType,
    string ParentObjectType,
    string ChildObjectType,
    string? QuantityAttributeKey,
    string? UnitAttributeKey,
    string? FindNumberAttributeKey,
    string? ReferenceDesignatorAttributeKey,
    string? LifecycleConstraintJson,
    bool RequiresApproval,
    string? AuditReferenceAttributeKey);

public sealed record OntologyVersionResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string VersionLabel,
    string? Summary,
    OntologyPublicationState State,
    int ObjectTypeCount,
    int RelationshipTypeCount,
    int BomRelationshipCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record OntologyVersionDetailResponse(
    OntologyVersionResponse Version,
    IReadOnlyCollection<ObjectTypeDefinitionResponse> ObjectTypes,
    IReadOnlyCollection<SemanticRelationshipDefinitionResponse> RelationshipTypes,
    IReadOnlyCollection<BomRelationshipDefinitionResponse> BomRelationships);

public sealed record SemanticLayerVersionResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string VersionLabel,
    string? Summary,
    Guid OntologyVersionId,
    string? OntologyVersionLabel,
    string? GraphNodeTypeMappingsJson,
    string? GraphRelationshipTypeMappingsJson,
    OntologyPublicationState State,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record LifecycleStateDefinitionResponse(
    Guid Id,
    string Key,
    string DisplayName,
    string? Category,
    int SortOrder,
    bool IsTerminal);

public sealed record LifecycleTransitionDefinitionResponse(
    Guid Id,
    string FromStateKey,
    string ToStateKey,
    bool RequiresApproval,
    string? SafeSummary);

public sealed record LifecycleVocabularyVersionResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string VersionLabel,
    string? Summary,
    OntologyPublicationState State,
    int StateCount,
    int TransitionCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record LifecycleVocabularyVersionDetailResponse(
    LifecycleVocabularyVersionResponse Version,
    IReadOnlyCollection<LifecycleStateDefinitionResponse> States,
    IReadOnlyCollection<LifecycleTransitionDefinitionResponse> Transitions);

public sealed record AttributeDefinitionResponse(
    Guid Id,
    string AttributeKey,
    string AppliesToObjectType,
    AttributeValueType ValueType,
    bool IsRequired,
    string? ValidationRulesJson,
    AttributeVisibility Visibility,
    string? RequiredPermissionKey,
    bool IsSearchable,
    bool IsAiFacing,
    string? ClassificationKey,
    string? DisplayName,
    string SafeSummary);

public sealed record AttributeSchemaVersionResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string VersionLabel,
    string? Summary,
    Guid OntologyVersionId,
    string? OntologyVersionLabel,
    OntologyPublicationState State,
    int AttributeCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record AttributeSchemaVersionDetailResponse(
    AttributeSchemaVersionResponse Version,
    IReadOnlyCollection<AttributeDefinitionResponse> Attributes);

public sealed record ModelPackageVersionResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string Name,
    string VersionLabel,
    string? Summary,
    Guid OntologyVersionId,
    string? OntologyVersionLabel,
    Guid SemanticLayerVersionId,
    string? SemanticLayerVersionLabel,
    Guid LifecycleVocabularyVersionId,
    string? LifecycleVocabularyVersionLabel,
    Guid AttributeSchemaVersionId,
    string? AttributeSchemaVersionLabel,
    Guid? ArtifactId,
    Guid? ArtifactVersionId,
    OntologyPublicationState State,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt);

public sealed record ModelPackagePreviewResponse(
    bool IsValid,
    IReadOnlyCollection<string> BlockingReasons,
    Guid OntologyVersionId,
    Guid SemanticLayerVersionId,
    Guid LifecycleVocabularyVersionId,
    Guid AttributeSchemaVersionId);
