using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue19ReviewTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_task_chain_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedTaskArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockingTaskArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BlockingCondition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_task_chain_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_task_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_task_comments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_review_task_chain_links_TenantId_BlockedTaskArtifactId_Reso~",
                table: "review_task_chain_links",
                columns: new[] { "TenantId", "BlockedTaskArtifactId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_review_task_chain_links_TenantId_BlockingTaskArtifactId_Res~",
                table: "review_task_chain_links",
                columns: new[] { "TenantId", "BlockingTaskArtifactId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_review_task_comments_TenantId_TaskArtifactId_CreatedAt",
                table: "review_task_comments",
                columns: new[] { "TenantId", "TaskArtifactId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_task_chain_links");

            migrationBuilder.DropTable(
                name: "review_task_comments");
        }
    }
}
