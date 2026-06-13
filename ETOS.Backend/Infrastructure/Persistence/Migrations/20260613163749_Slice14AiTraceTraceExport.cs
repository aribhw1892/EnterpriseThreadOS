using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice14AiTraceTraceExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_trace_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetrievalRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    TraceKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IntentKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StrategyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    QueryText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourcesSummaryJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    FilteredSummariesJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    DeniedSafeSummariesJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    SensitiveDeniedReferencesJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    ConfidenceImpactJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PromptTemplateVersionLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OutputSchemaVersionLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    GeneratedOutputJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_trace_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_trace_artifact_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiTraceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ObjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_trace_artifact_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_trace_artifact_links_ai_trace_records_AiTraceRecordId",
                        column: x => x.AiTraceRecordId,
                        principalTable: "ai_trace_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_trace_export_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiTraceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RedactionMetadataJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    EvidenceLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_trace_export_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_trace_export_records_ai_trace_records_AiTraceRecordId",
                        column: x => x.AiTraceRecordId,
                        principalTable: "ai_trace_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_artifact_links_AiTraceRecordId",
                table: "ai_trace_artifact_links",
                column: "AiTraceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_artifact_links_TenantId_AiTraceRecordId_LinkKind",
                table: "ai_trace_artifact_links",
                columns: new[] { "TenantId", "AiTraceRecordId", "LinkKind" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_export_records_AiTraceRecordId",
                table: "ai_trace_export_records",
                column: "AiTraceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_export_records_TenantId_AiTraceRecordId_CreatedAt",
                table: "ai_trace_export_records",
                columns: new[] { "TenantId", "AiTraceRecordId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_records_TenantId_CreatedAt",
                table: "ai_trace_records",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_records_TenantId_RetrievalRunId",
                table: "ai_trace_records",
                columns: new[] { "TenantId", "RetrievalRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_trace_artifact_links");

            migrationBuilder.DropTable(
                name: "ai_trace_export_records");

            migrationBuilder.DropTable(
                name: "ai_trace_records");
        }
    }
}
