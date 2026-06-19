using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue23AgentRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentRunId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agent_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPreview = table.Column<bool>(type: "boolean", nullable: false),
                    IsDryRun = table.Column<bool>(type: "boolean", nullable: false),
                    SafeModeApplied = table.Column<bool>(type: "boolean", nullable: false),
                    InputSafeSummaryJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    OutputSafeSummaryJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    StructuredOutputJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    DerivedRiskSnapshotJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    FallbackUsedJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ValidationResultJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ErrorSafeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GovernedContextSummaryJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    RetrievalRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecommendationArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiTraceRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_records_TenantId_AgentRunId",
                table: "ai_trace_records",
                columns: new[] { "TenantId", "AgentRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_TenantId_AgentVersionId_StartedAt",
                table: "agent_runs",
                columns: new[] { "TenantId", "AgentVersionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_TenantId_StartedAt",
                table: "agent_runs",
                columns: new[] { "TenantId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_runs");

            migrationBuilder.DropIndex(
                name: "IX_ai_trace_records_TenantId_AgentRunId",
                table: "ai_trace_records");

            migrationBuilder.DropColumn(
                name: "AgentRunId",
                table: "ai_trace_records");
        }
    }
}
