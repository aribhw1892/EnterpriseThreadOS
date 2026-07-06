using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue20DecisionsOutcomesLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "decision_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_comments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decision_learning_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PatternKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OutcomeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EvidenceSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_learning_evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decision_votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vote = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_votes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outcome_check_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActualOutcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OutcomeStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OutcomeConfidence = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: true),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EvidenceSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outcome_check_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_decision_comments_TenantId_DecisionArtifactId_CreatedAt",
                table: "decision_comments",
                columns: new[] { "TenantId", "DecisionArtifactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_decision_learning_evidence_TenantId_PatternKey_CreatedAt",
                table: "decision_learning_evidence",
                columns: new[] { "TenantId", "PatternKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_decision_votes_TenantId_DecisionArtifactId_UserId",
                table: "decision_votes",
                columns: new[] { "TenantId", "DecisionArtifactId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outcome_check_runs_TenantId_DecisionArtifactId_MeasuredAt",
                table: "outcome_check_runs",
                columns: new[] { "TenantId", "DecisionArtifactId", "MeasuredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "decision_comments");

            migrationBuilder.DropTable(
                name: "decision_learning_evidence");

            migrationBuilder.DropTable(
                name: "decision_votes");

            migrationBuilder.DropTable(
                name: "outcome_check_runs");
        }
    }
}
