using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice3AuditSecurityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceObjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceObjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PolicyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RetentionCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RetainUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsArchiveEligible = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceAction = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RelatedAuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewTaskReady = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewTaskHint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewTaskCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_events_audit_records_RelatedAuditRecordId",
                        column: x => x.RelatedAuditRecordId,
                        principalTable: "audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_TenantId_Action_CreatedAt",
                table: "audit_records",
                columns: new[] { "TenantId", "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_TenantId_CreatedAt",
                table: "audit_records",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_TenantId_Result_CreatedAt",
                table: "audit_records",
                columns: new[] { "TenantId", "Result", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_RelatedAuditRecordId",
                table: "security_events",
                column: "RelatedAuditRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_TenantId_CreatedAt",
                table: "security_events",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_TenantId_EventType_CreatedAt",
                table: "security_events",
                columns: new[] { "TenantId", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_TenantId_Severity_CreatedAt",
                table: "security_events",
                columns: new[] { "TenantId", "Severity", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_events");

            migrationBuilder.DropTable(
                name: "audit_records");
        }
    }
}
