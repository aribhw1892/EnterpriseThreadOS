using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice17DashboardReportExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dashboard_report_export_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RedactionMetadataJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    EvidenceLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_report_export_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_report_export_records_TenantId_ArtifactId_Artifac~",
                table: "dashboard_report_export_records",
                columns: new[] { "TenantId", "ArtifactId", "ArtifactVersionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_report_export_records");
        }
    }
}
