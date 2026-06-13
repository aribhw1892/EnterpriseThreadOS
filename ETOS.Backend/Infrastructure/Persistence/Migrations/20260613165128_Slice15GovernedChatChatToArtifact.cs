using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice15GovernedChatChatToArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GovernedChatTurnId",
                table: "ai_trace_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "governed_chat_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartGraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTurnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governed_chat_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "governed_chat_turns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AssistantSafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RetrievalRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiTraceRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptTemplateArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptTemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputSchemaArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputSchemaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedOutputJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    ConfidenceJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DraftArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftArtifactVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftArtifactKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governed_chat_turns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_governed_chat_turns_governed_chat_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "governed_chat_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_governed_chat_sessions_TenantId_CreatedAt",
                table: "governed_chat_sessions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_governed_chat_turns_SessionId",
                table: "governed_chat_turns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_governed_chat_turns_TenantId_CreatedAt",
                table: "governed_chat_turns",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_governed_chat_turns_TenantId_SessionId_CreatedAt",
                table: "governed_chat_turns",
                columns: new[] { "TenantId", "SessionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "governed_chat_turns");

            migrationBuilder.DropTable(
                name: "governed_chat_sessions");

            migrationBuilder.DropColumn(
                name: "GovernedChatTurnId",
                table: "ai_trace_records");
        }
    }
}
