using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice7CanonicalOntology : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lifecycle_vocabulary_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lifecycle_vocabulary_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ontology_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ontology_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lifecycle_state_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleVocabularyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsTerminal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lifecycle_state_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lifecycle_state_definitions_lifecycle_vocabulary_versions_L~",
                        column: x => x.LifecycleVocabularyVersionId,
                        principalTable: "lifecycle_vocabulary_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lifecycle_transition_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleVocabularyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedFromStateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ToStateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedToStateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lifecycle_transition_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lifecycle_transition_definitions_lifecycle_vocabulary_versi~",
                        column: x => x.LifecycleVocabularyVersionId,
                        principalTable: "lifecycle_vocabulary_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_schema_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OntologyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_schema_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attribute_schema_versions_ontology_versions_OntologyVersion~",
                        column: x => x.OntologyVersionId,
                        principalTable: "ontology_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bom_relationship_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OntologyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedRelationshipType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ParentObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedParentObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ChildObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedChildObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    QuantityAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    UnitAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FindNumberAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReferenceDesignatorAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    LifecycleConstraintJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AuditReferenceAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_relationship_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bom_relationship_definitions_ontology_versions_OntologyVers~",
                        column: x => x.OntologyVersionId,
                        principalTable: "ontology_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ontology_object_type_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OntologyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VersionIdentityFieldsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ontology_object_type_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ontology_object_type_definitions_ontology_versions_Ontology~",
                        column: x => x.OntologyVersionId,
                        principalTable: "ontology_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "semantic_layer_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OntologyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GraphNodeTypeMappingsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    GraphRelationshipTypeMappingsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semantic_layer_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_semantic_layer_versions_ontology_versions_OntologyVersionId",
                        column: x => x.OntologyVersionId,
                        principalTable: "ontology_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "semantic_relationship_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OntologyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedRelationshipType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FromObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedFromObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ToObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedToObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsVersionRelationship = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semantic_relationship_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_semantic_relationship_definitions_ontology_versions_Ontolog~",
                        column: x => x.OntologyVersionId,
                        principalTable: "ontology_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeSchemaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AppliesToObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedAppliesToObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationRulesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredPermissionKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsSearchable = table.Column<bool>(type: "boolean", nullable: false),
                    IsAiFacing = table.Column<bool>(type: "boolean", nullable: false),
                    ClassificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attribute_definitions_attribute_schema_versions_AttributeSc~",
                        column: x => x.AttributeSchemaVersionId,
                        principalTable: "attribute_schema_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "model_package_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OntologyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SemanticLayerVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleVocabularyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeSchemaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_package_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_model_package_versions_attribute_schema_versions_AttributeS~",
                        column: x => x.AttributeSchemaVersionId,
                        principalTable: "attribute_schema_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_package_versions_lifecycle_vocabulary_versions_Lifecy~",
                        column: x => x.LifecycleVocabularyVersionId,
                        principalTable: "lifecycle_vocabulary_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_package_versions_ontology_versions_OntologyVersionId",
                        column: x => x.OntologyVersionId,
                        principalTable: "ontology_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_package_versions_semantic_layer_versions_SemanticLaye~",
                        column: x => x.SemanticLayerVersionId,
                        principalTable: "semantic_layer_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attribute_definitions_AttributeSchemaVersionId",
                table: "attribute_definitions",
                column: "AttributeSchemaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_definitions_TenantId_AttributeSchemaVersionId_Nor~",
                table: "attribute_definitions",
                columns: new[] { "TenantId", "AttributeSchemaVersionId", "NormalizedAppliesToObjectType", "NormalizedAttributeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attribute_schema_versions_OntologyVersionId",
                table: "attribute_schema_versions",
                column: "OntologyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_schema_versions_TenantId_NormalizedKey_Normalized~",
                table: "attribute_schema_versions",
                columns: new[] { "TenantId", "NormalizedKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attribute_schema_versions_TenantId_NormalizedKey_State_Publ~",
                table: "attribute_schema_versions",
                columns: new[] { "TenantId", "NormalizedKey", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_relationship_definitions_OntologyVersionId",
                table: "bom_relationship_definitions",
                column: "OntologyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_bom_relationship_definitions_TenantId_OntologyVersionId_Nor~",
                table: "bom_relationship_definitions",
                columns: new[] { "TenantId", "OntologyVersionId", "NormalizedRelationshipType", "NormalizedParentObjectType", "NormalizedChildObjectType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_state_definitions_LifecycleVocabularyVersionId",
                table: "lifecycle_state_definitions",
                column: "LifecycleVocabularyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_state_definitions_TenantId_LifecycleVocabularyVer~",
                table: "lifecycle_state_definitions",
                columns: new[] { "TenantId", "LifecycleVocabularyVersionId", "NormalizedKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_transition_definitions_LifecycleVocabularyVersion~",
                table: "lifecycle_transition_definitions",
                column: "LifecycleVocabularyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_transition_definitions_TenantId_LifecycleVocabula~",
                table: "lifecycle_transition_definitions",
                columns: new[] { "TenantId", "LifecycleVocabularyVersionId", "NormalizedFromStateKey", "NormalizedToStateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_vocabulary_versions_TenantId_NormalizedKey_Normal~",
                table: "lifecycle_vocabulary_versions",
                columns: new[] { "TenantId", "NormalizedKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_vocabulary_versions_TenantId_NormalizedKey_State_~",
                table: "lifecycle_vocabulary_versions",
                columns: new[] { "TenantId", "NormalizedKey", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_model_package_versions_AttributeSchemaVersionId",
                table: "model_package_versions",
                column: "AttributeSchemaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_model_package_versions_LifecycleVocabularyVersionId",
                table: "model_package_versions",
                column: "LifecycleVocabularyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_model_package_versions_OntologyVersionId",
                table: "model_package_versions",
                column: "OntologyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_model_package_versions_SemanticLayerVersionId",
                table: "model_package_versions",
                column: "SemanticLayerVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_model_package_versions_TenantId_NormalizedKey_NormalizedVer~",
                table: "model_package_versions",
                columns: new[] { "TenantId", "NormalizedKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_package_versions_TenantId_NormalizedKey_State_Publish~",
                table: "model_package_versions",
                columns: new[] { "TenantId", "NormalizedKey", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ontology_object_type_definitions_OntologyVersionId",
                table: "ontology_object_type_definitions",
                column: "OntologyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ontology_object_type_definitions_TenantId_OntologyVersionId~",
                table: "ontology_object_type_definitions",
                columns: new[] { "TenantId", "OntologyVersionId", "NormalizedKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ontology_versions_TenantId_NormalizedKey_NormalizedVersionL~",
                table: "ontology_versions",
                columns: new[] { "TenantId", "NormalizedKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ontology_versions_TenantId_NormalizedKey_State_PublishedAt",
                table: "ontology_versions",
                columns: new[] { "TenantId", "NormalizedKey", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_semantic_layer_versions_OntologyVersionId",
                table: "semantic_layer_versions",
                column: "OntologyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_semantic_layer_versions_TenantId_NormalizedKey_NormalizedVe~",
                table: "semantic_layer_versions",
                columns: new[] { "TenantId", "NormalizedKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_semantic_layer_versions_TenantId_NormalizedKey_State_Publis~",
                table: "semantic_layer_versions",
                columns: new[] { "TenantId", "NormalizedKey", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_semantic_relationship_definitions_OntologyVersionId",
                table: "semantic_relationship_definitions",
                column: "OntologyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_semantic_relationship_definitions_TenantId_OntologyVersionI~",
                table: "semantic_relationship_definitions",
                columns: new[] { "TenantId", "OntologyVersionId", "NormalizedRelationshipType", "NormalizedFromObjectType", "NormalizedToObjectType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attribute_definitions");

            migrationBuilder.DropTable(
                name: "bom_relationship_definitions");

            migrationBuilder.DropTable(
                name: "lifecycle_state_definitions");

            migrationBuilder.DropTable(
                name: "lifecycle_transition_definitions");

            migrationBuilder.DropTable(
                name: "model_package_versions");

            migrationBuilder.DropTable(
                name: "ontology_object_type_definitions");

            migrationBuilder.DropTable(
                name: "semantic_relationship_definitions");

            migrationBuilder.DropTable(
                name: "attribute_schema_versions");

            migrationBuilder.DropTable(
                name: "lifecycle_vocabulary_versions");

            migrationBuilder.DropTable(
                name: "semantic_layer_versions");

            migrationBuilder.DropTable(
                name: "ontology_versions");
        }
    }
}
