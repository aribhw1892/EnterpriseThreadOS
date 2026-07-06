using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue24WorkflowRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentWorkflowRunId",
                table: "tool_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowRunId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentWorkflowRunId",
                table: "agent_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "safe_mode_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EventKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PolicyRuleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BlockedAction = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToolRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewTaskArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_safe_mode_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPreview = table.Column<bool>(type: "boolean", nullable: false),
                    SafeModeApplied = table.Column<bool>(type: "boolean", nullable: false),
                    PartialCompletion = table.Column<bool>(type: "boolean", nullable: false),
                    InputSafeSummaryJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    OutputSafeSummaryJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    StepResultsJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    InheritedRiskSnapshotJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    RuntimeTrustRecalculationJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    RecommendationArtifactIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReviewTaskArtifactIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiTraceRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_runs_TenantId_ParentWorkflowRunId_CreatedAt",
                table: "tool_runs",
                columns: new[] { "TenantId", "ParentWorkflowRunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_trace_records_TenantId_WorkflowRunId",
                table: "ai_trace_records",
                columns: new[] { "TenantId", "WorkflowRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_TenantId_ParentWorkflowRunId_StartedAt",
                table: "agent_runs",
                columns: new[] { "TenantId", "ParentWorkflowRunId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_safe_mode_events_TenantId_StepKey_CreatedAt",
                table: "safe_mode_events",
                columns: new[] { "TenantId", "StepKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_safe_mode_events_TenantId_WorkflowRunId_CreatedAt",
                table: "safe_mode_events",
                columns: new[] { "TenantId", "WorkflowRunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_TenantId_StartedAt",
                table: "workflow_runs",
                columns: new[] { "TenantId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_TenantId_WorkflowVersionId_StartedAt",
                table: "workflow_runs",
                columns: new[] { "TenantId", "WorkflowVersionId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "safe_mode_events");

            migrationBuilder.DropTable(
                name: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "IX_tool_runs_TenantId_ParentWorkflowRunId_CreatedAt",
                table: "tool_runs");

            migrationBuilder.DropIndex(
                name: "IX_ai_trace_records_TenantId_WorkflowRunId",
                table: "ai_trace_records");

            migrationBuilder.DropIndex(
                name: "IX_agent_runs_TenantId_ParentWorkflowRunId_StartedAt",
                table: "agent_runs");

            migrationBuilder.DropColumn(
                name: "ParentWorkflowRunId",
                table: "tool_runs");

            migrationBuilder.DropColumn(
                name: "WorkflowRunId",
                table: "ai_trace_records");

            migrationBuilder.DropColumn(
                name: "ParentWorkflowRunId",
                table: "agent_runs");
        }
    }
}
