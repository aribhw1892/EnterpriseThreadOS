using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue22ToolRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RetrievalRunId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContextPackageId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ToolRunId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tool_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDryRun = table.Column<bool>(type: "boolean", nullable: false),
                    InputSafeSummaryJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    OutputSafeSummaryJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    ValidationResultJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CompatibilityNotesJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ErrorSafeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConnectorCredentialSafeSummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RetrievalRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiTraceRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_records_TenantId_ToolRunId",
                table: "ai_trace_records",
                columns: new[] { "TenantId", "ToolRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_runs_TenantId_CreatedAt",
                table: "tool_runs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_runs_TenantId_ToolDefinitionVersionId_CreatedAt",
                table: "tool_runs",
                columns: new[] { "TenantId", "ToolDefinitionVersionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_runs");

            migrationBuilder.DropIndex(
                name: "IX_ai_trace_records_TenantId_ToolRunId",
                table: "ai_trace_records");

            migrationBuilder.DropColumn(
                name: "ToolRunId",
                table: "ai_trace_records");

            migrationBuilder.AlterColumn<Guid>(
                name: "RetrievalRunId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContextPackageId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
