using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue12DocumentMemoryObjectLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedDocumentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClassificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedClassificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_artifacts_artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExtractedMetadataSummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExtractionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtractionFailureSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_versions_document_artifacts_DocumentArtifactId",
                        column: x => x.DocumentArtifactId,
                        principalTable: "document_artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_object_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    EvidenceSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExtractionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_object_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_object_links_document_artifacts_DocumentArtifactId",
                        column: x => x.DocumentArtifactId,
                        principalTable: "document_artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_object_links_document_versions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_object_links_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_vector_index_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TenantFilter = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PolicyFilterSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FailureSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_vector_index_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_vector_index_records_document_artifacts_DocumentAr~",
                        column: x => x.DocumentArtifactId,
                        principalTable: "document_artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_vector_index_records_document_versions_DocumentVer~",
                        column: x => x.DocumentVersionId,
                        principalTable: "document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_artifacts_ArtifactId",
                table: "document_artifacts",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_document_artifacts_TenantId_NormalizedClassificationKey",
                table: "document_artifacts",
                columns: new[] { "TenantId", "NormalizedClassificationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_document_artifacts_TenantId_NormalizedDocumentType_CreatedAt",
                table: "document_artifacts",
                columns: new[] { "TenantId", "NormalizedDocumentType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_object_links_DocumentArtifactId",
                table: "document_object_links",
                column: "DocumentArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_document_object_links_DocumentVersionId",
                table: "document_object_links",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_document_object_links_ImportBatchId",
                table: "document_object_links",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_document_object_links_TenantId_DocumentArtifactId_CreatedAt",
                table: "document_object_links",
                columns: new[] { "TenantId", "DocumentArtifactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_object_links_TenantId_GraphNodeId",
                table: "document_object_links",
                columns: new[] { "TenantId", "GraphNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_object_links_TenantId_ImportBatchId",
                table: "document_object_links",
                columns: new[] { "TenantId", "ImportBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_vector_index_records_DocumentArtifactId",
                table: "document_vector_index_records",
                column: "DocumentArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_document_vector_index_records_DocumentVersionId",
                table: "document_vector_index_records",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_document_vector_index_records_TenantId_DocumentArtifactId_D~",
                table: "document_vector_index_records",
                columns: new[] { "TenantId", "DocumentArtifactId", "DocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_vector_index_records_TenantId_Status_CreatedAt",
                table: "document_vector_index_records",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_DocumentArtifactId_NormalizedVersionLabel",
                table: "document_versions",
                columns: new[] { "DocumentArtifactId", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_TenantId_DocumentArtifactId_CreatedAt",
                table: "document_versions",
                columns: new[] { "TenantId", "DocumentArtifactId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_object_links");

            migrationBuilder.DropTable(
                name: "document_vector_index_records");

            migrationBuilder.DropTable(
                name: "document_versions");

            migrationBuilder.DropTable(
                name: "document_artifacts");
        }
    }
}
