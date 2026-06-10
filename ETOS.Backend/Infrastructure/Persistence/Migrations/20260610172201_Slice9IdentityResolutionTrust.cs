using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice9IdentityResolutionTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identity_resolution_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdentityAttributeKeysJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AutoApproveThreshold = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    ReviewThreshold = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_resolution_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "identity_candidate_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportStagingGraphRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityResolutionRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceGraphNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetGraphNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceRecordId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TargetRecordId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdentityKey = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrustState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExcludedFromTrustedRecommendations = table.Column<bool>(type: "boolean", nullable: false),
                    GraphRelationshipId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_candidate_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_candidate_links_identity_resolution_rules_Identity~",
                        column: x => x.IdentityResolutionRuleId,
                        principalTable: "identity_resolution_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_identity_candidate_links_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_identity_candidate_links_import_mapping_versions_ImportMapp~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_identity_candidate_links_import_staging_graph_runs_ImportSt~",
                        column: x => x.ImportStagingGraphRunId,
                        principalTable: "import_staging_graph_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "identity_resolution_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityCandidateLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultingTrustState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_resolution_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_resolution_decisions_identity_candidate_links_Iden~",
                        column: x => x.IdentityCandidateLinkId,
                        principalTable: "identity_candidate_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trust_score_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityCandidateLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphRelationshipId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    TrustState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BreakdownJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trust_score_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trust_score_records_identity_candidate_links_IdentityCandid~",
                        column: x => x.IdentityCandidateLinkId,
                        principalTable: "identity_candidate_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_trust_score_records_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identity_learning_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityCandidateLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityResolutionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdentityKey = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    EvidenceSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_learning_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_learning_evidence_identity_candidate_links_Identit~",
                        column: x => x.IdentityCandidateLinkId,
                        principalTable: "identity_candidate_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_identity_learning_evidence_identity_resolution_decisions_Id~",
                        column: x => x.IdentityResolutionDecisionId,
                        principalTable: "identity_resolution_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_identity_candidate_links_IdentityResolutionRuleId",
                table: "identity_candidate_links",
                column: "IdentityResolutionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_candidate_links_ImportBatchId",
                table: "identity_candidate_links",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_candidate_links_ImportMappingVersionId",
                table: "identity_candidate_links",
                column: "ImportMappingVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_candidate_links_ImportStagingGraphRunId",
                table: "identity_candidate_links",
                column: "ImportStagingGraphRunId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_candidate_links_TenantId_ImportBatchId_State",
                table: "identity_candidate_links",
                columns: new[] { "TenantId", "ImportBatchId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_identity_candidate_links_TenantId_SourceGraphNodeId_TargetG~",
                table: "identity_candidate_links",
                columns: new[] { "TenantId", "SourceGraphNodeId", "TargetGraphNodeId", "IdentityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_learning_evidence_IdentityCandidateLinkId",
                table: "identity_learning_evidence",
                column: "IdentityCandidateLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_learning_evidence_IdentityResolutionDecisionId",
                table: "identity_learning_evidence",
                column: "IdentityResolutionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_learning_evidence_TenantId_Outcome_CreatedAt",
                table: "identity_learning_evidence",
                columns: new[] { "TenantId", "Outcome", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_identity_resolution_decisions_IdentityCandidateLinkId",
                table: "identity_resolution_decisions",
                column: "IdentityCandidateLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_resolution_decisions_TenantId_IdentityCandidateLin~",
                table: "identity_resolution_decisions",
                columns: new[] { "TenantId", "IdentityCandidateLinkId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_identity_resolution_rules_TenantId_NormalizedName",
                table: "identity_resolution_rules",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_resolution_rules_TenantId_NormalizedObjectType_IsA~",
                table: "identity_resolution_rules",
                columns: new[] { "TenantId", "NormalizedObjectType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_trust_score_records_IdentityCandidateLinkId",
                table: "trust_score_records",
                column: "IdentityCandidateLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_trust_score_records_ImportBatchId",
                table: "trust_score_records",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_trust_score_records_TenantId_ImportBatchId_EntityType",
                table: "trust_score_records",
                columns: new[] { "TenantId", "ImportBatchId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_trust_score_records_TenantId_ImportBatchId_IdentityCandidat~",
                table: "trust_score_records",
                columns: new[] { "TenantId", "ImportBatchId", "IdentityCandidateLinkId", "EntityType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_learning_evidence");

            migrationBuilder.DropTable(
                name: "trust_score_records");

            migrationBuilder.DropTable(
                name: "identity_resolution_decisions");

            migrationBuilder.DropTable(
                name: "identity_candidate_links");

            migrationBuilder.DropTable(
                name: "identity_resolution_rules");
        }
    }
}
