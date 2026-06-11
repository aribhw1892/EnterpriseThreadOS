using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue11TrustedGraphPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bom_comparison_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceContext = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CadSummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EbomSummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MissingInCadCount = table.Column<int>(type: "integer", nullable: false),
                    MissingInEbomCount = table.Column<int>(type: "integer", nullable: false),
                    QuantityMismatchCount = table.Column<int>(type: "integer", nullable: false),
                    UsageReferenceMismatchCount = table.Column<int>(type: "integer", nullable: false),
                    UnresolvedIdentityCount = table.Column<int>(type: "integer", nullable: false),
                    ResultJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_comparison_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bom_comparison_runs_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "graph_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GraphSpace = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    RelationshipCount = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_graph_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_promotion_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportStagingGraphRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PromotedNodeCount = table.Column<int>(type: "integer", nullable: false),
                    PromotedRelationshipCount = table.Column<int>(type: "integer", nullable: false),
                    SourceEvidenceIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_promotion_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_promotion_runs_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_promotion_runs_import_staging_graph_runs_ImportStagi~",
                        column: x => x.ImportStagingGraphRunId,
                        principalTable: "import_staging_graph_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rejected_staging_summaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportStagingGraphRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidationSummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DecisionSummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    RelationshipCount = table.Column<int>(type: "integer", nullable: false),
                    SourceEvidenceIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejected_staging_summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rejected_staging_summaries_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rejected_staging_summaries_import_staging_graph_runs_Import~",
                        column: x => x.ImportStagingGraphRunId,
                        principalTable: "import_staging_graph_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "graph_diffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiffJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_graph_diffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_graph_diffs_graph_snapshots_FromSnapshotId",
                        column: x => x.FromSnapshotId,
                        principalTable: "graph_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_graph_diffs_graph_snapshots_ToSnapshotId",
                        column: x => x.ToSnapshotId,
                        principalTable: "graph_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bom_comparison_runs_ImportBatchId",
                table: "bom_comparison_runs",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_bom_comparison_runs_TenantId_ImportBatchId_CreatedAt",
                table: "bom_comparison_runs",
                columns: new[] { "TenantId", "ImportBatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_graph_diffs_FromSnapshotId",
                table: "graph_diffs",
                column: "FromSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_graph_diffs_TenantId_FromSnapshotId_ToSnapshotId_CreatedAt",
                table: "graph_diffs",
                columns: new[] { "TenantId", "FromSnapshotId", "ToSnapshotId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_graph_diffs_ToSnapshotId",
                table: "graph_diffs",
                column: "ToSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_graph_snapshots_TenantId_ChecksumSha256",
                table: "graph_snapshots",
                columns: new[] { "TenantId", "ChecksumSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_graph_snapshots_TenantId_GraphSpace_CreatedAt",
                table: "graph_snapshots",
                columns: new[] { "TenantId", "GraphSpace", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_promotion_runs_ImportBatchId",
                table: "import_promotion_runs",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_import_promotion_runs_ImportStagingGraphRunId",
                table: "import_promotion_runs",
                column: "ImportStagingGraphRunId");

            migrationBuilder.CreateIndex(
                name: "IX_import_promotion_runs_TenantId_ImportBatchId_CreatedAt",
                table: "import_promotion_runs",
                columns: new[] { "TenantId", "ImportBatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rejected_staging_summaries_ImportBatchId",
                table: "rejected_staging_summaries",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_staging_summaries_ImportStagingGraphRunId",
                table: "rejected_staging_summaries",
                column: "ImportStagingGraphRunId");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_staging_summaries_TenantId_ImportBatchId_CreatedAt",
                table: "rejected_staging_summaries",
                columns: new[] { "TenantId", "ImportBatchId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bom_comparison_runs");

            migrationBuilder.DropTable(
                name: "graph_diffs");

            migrationBuilder.DropTable(
                name: "import_promotion_runs");

            migrationBuilder.DropTable(
                name: "rejected_staging_summaries");

            migrationBuilder.DropTable(
                name: "graph_snapshots");
        }
    }
}
