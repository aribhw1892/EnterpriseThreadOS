using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice10DataQualityIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_quality_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssueCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedIssueCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AffectedEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportStagingGraphRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportValidationIssueId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportFileEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityCandidateLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecurityEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphRelationshipId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrustImpactPenalty = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    ResultingTrustState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExcludedFromTrustedRecommendations = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewPriority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewTaskReady = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewTaskHint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewHookCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UniqueSourceKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EvidenceSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_quality_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_identity_candidate_links_IdentityCandid~",
                        column: x => x.IdentityCandidateLinkId,
                        principalTable: "identity_candidate_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_import_file_evidence_ImportFileEvidence~",
                        column: x => x.ImportFileEvidenceId,
                        principalTable: "import_file_evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_import_mapping_versions_ImportMappingVe~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_import_staging_graph_runs_ImportStaging~",
                        column: x => x.ImportStagingGraphRunId,
                        principalTable: "import_staging_graph_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_import_validation_issues_ImportValidati~",
                        column: x => x.ImportValidationIssueId,
                        principalTable: "import_validation_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_quality_issues_security_events_SecurityEventId",
                        column: x => x.SecurityEventId,
                        principalTable: "security_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_issue_type_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueTypeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedIssueTypeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsLiveSourceScanning = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_issue_type_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_quality_issue_source_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataQualityIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_quality_issue_source_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_data_quality_issue_source_links_data_quality_issues_DataQua~",
                        column: x => x.DataQualityIssueId,
                        principalTable: "data_quality_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_quality_trust_impacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataQualityIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphRelationshipId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityCandidateLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScorePenalty = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    ResultingTrustState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExcludedFromTrustedRecommendations = table.Column<bool>(type: "boolean", nullable: false),
                    BreakdownJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_quality_trust_impacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_data_quality_trust_impacts_data_quality_issues_DataQualityI~",
                        column: x => x.DataQualityIssueId,
                        principalTable: "data_quality_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issue_source_links_DataQualityIssueId",
                table: "data_quality_issue_source_links",
                column: "DataQualityIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issue_source_links_TenantId_DataQualityIssueId~",
                table: "data_quality_issue_source_links",
                columns: new[] { "TenantId", "DataQualityIssueId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_IdentityCandidateLinkId",
                table: "data_quality_issues",
                column: "IdentityCandidateLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_ImportBatchId",
                table: "data_quality_issues",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_ImportFileEvidenceId",
                table: "data_quality_issues",
                column: "ImportFileEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_ImportMappingVersionId",
                table: "data_quality_issues",
                column: "ImportMappingVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_ImportStagingGraphRunId",
                table: "data_quality_issues",
                column: "ImportStagingGraphRunId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_ImportValidationIssueId",
                table: "data_quality_issues",
                column: "ImportValidationIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_SecurityEventId",
                table: "data_quality_issues",
                column: "SecurityEventId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_TenantId_NormalizedIssueCode",
                table: "data_quality_issues",
                columns: new[] { "TenantId", "NormalizedIssueCode" });

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_TenantId_Origin_CreatedAt",
                table: "data_quality_issues",
                columns: new[] { "TenantId", "Origin", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_TenantId_Status_Severity_CreatedAt",
                table: "data_quality_issues",
                columns: new[] { "TenantId", "Status", "Severity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_issues_TenantId_UniqueSourceKey",
                table: "data_quality_issues",
                columns: new[] { "TenantId", "UniqueSourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_trust_impacts_DataQualityIssueId",
                table: "data_quality_trust_impacts",
                column: "DataQualityIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_data_quality_trust_impacts_TenantId_DataQualityIssueId_Targ~",
                table: "data_quality_trust_impacts",
                columns: new[] { "TenantId", "DataQualityIssueId", "TargetEntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_issue_type_definitions_TenantId_NormalizedIssueT~",
                table: "monitoring_issue_type_definitions",
                columns: new[] { "TenantId", "NormalizedIssueTypeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_quality_issue_source_links");

            migrationBuilder.DropTable(
                name: "data_quality_trust_impacts");

            migrationBuilder.DropTable(
                name: "monitoring_issue_type_definitions");

            migrationBuilder.DropTable(
                name: "data_quality_issues");
        }
    }
}
