using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice8ImportMappingStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedSourceSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActiveModelPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveModelPackageKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActiveModelPackageVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StagedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_batches_model_package_versions_ActiveModelPackageVer~",
                        column: x => x.ActiveModelPackageVersionId,
                        principalTable: "model_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_file_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_file_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_file_evidence_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_mapping_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SuggestionProvider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_mapping_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_mapping_versions_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_mapping_versions_model_package_versions_ModelPackage~",
                        column: x => x.ModelPackageVersionId,
                        principalTable: "model_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_column_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceColumn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedSourceColumn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CanonicalObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedCanonicalObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CanonicalAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NormalizedCanonicalAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsIdentityField = table.Column<bool>(type: "boolean", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_column_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_column_mappings_import_mapping_versions_ImportMappin~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_lifecycle_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedSourceValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CanonicalLifecycleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedCanonicalLifecycleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_lifecycle_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_lifecycle_mappings_import_mapping_versions_ImportMap~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_staging_graph_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    RelationshipCount = table.Column<int>(type: "integer", nullable: false),
                    GraphNodeIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    GraphRelationshipIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    FailureSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_staging_graph_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_staging_graph_runs_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_staging_graph_runs_import_mapping_versions_ImportMap~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_validation_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: true),
                    SourceColumn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CanonicalObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IssueCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_validation_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_validation_issues_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_validation_issues_import_mapping_versions_ImportMapp~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_ActiveModelPackageVersionId",
                table: "import_batches",
                column: "ActiveModelPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_TenantId_CreatedAt",
                table: "import_batches",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_TenantId_NormalizedSourceSystem_CreatedAt",
                table: "import_batches",
                columns: new[] { "TenantId", "NormalizedSourceSystem", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_column_mappings_ImportMappingVersionId_NormalizedSou~",
                table: "import_column_mappings",
                columns: new[] { "ImportMappingVersionId", "NormalizedSourceColumn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_file_evidence_ImportBatchId",
                table: "import_file_evidence",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_import_file_evidence_TenantId_ImportBatchId_CreatedAt",
                table: "import_file_evidence",
                columns: new[] { "TenantId", "ImportBatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_file_evidence_TenantId_Sha256Checksum",
                table: "import_file_evidence",
                columns: new[] { "TenantId", "Sha256Checksum" });

            migrationBuilder.CreateIndex(
                name: "IX_import_lifecycle_mappings_ImportMappingVersionId_Normalized~",
                table: "import_lifecycle_mappings",
                columns: new[] { "ImportMappingVersionId", "NormalizedSourceValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_mapping_versions_ImportBatchId_NormalizedVersionLabel",
                table: "import_mapping_versions",
                columns: new[] { "ImportBatchId", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_mapping_versions_ModelPackageVersionId",
                table: "import_mapping_versions",
                column: "ModelPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_import_mapping_versions_TenantId_State_CreatedAt",
                table: "import_mapping_versions",
                columns: new[] { "TenantId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_graph_runs_ImportBatchId",
                table: "import_staging_graph_runs",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_graph_runs_ImportMappingVersionId",
                table: "import_staging_graph_runs",
                column: "ImportMappingVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_graph_runs_TenantId_ImportBatchId_CreatedAt",
                table: "import_staging_graph_runs",
                columns: new[] { "TenantId", "ImportBatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_validation_issues_ImportBatchId",
                table: "import_validation_issues",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_import_validation_issues_ImportMappingVersionId",
                table: "import_validation_issues",
                column: "ImportMappingVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_import_validation_issues_TenantId_ImportBatchId_Severity",
                table: "import_validation_issues",
                columns: new[] { "TenantId", "ImportBatchId", "Severity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_column_mappings");

            migrationBuilder.DropTable(
                name: "import_file_evidence");

            migrationBuilder.DropTable(
                name: "import_lifecycle_mappings");

            migrationBuilder.DropTable(
                name: "import_staging_graph_runs");

            migrationBuilder.DropTable(
                name: "import_validation_issues");

            migrationBuilder.DropTable(
                name: "import_mapping_versions");

            migrationBuilder.DropTable(
                name: "import_batches");
        }
    }
}
