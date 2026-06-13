using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice13GovernedQueryContextAssembly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "query_intent_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedIntentKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntentKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_query_intent_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_strategy_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedStrategyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GraphSpace = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredTrustState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RelationshipTypesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AllowsSemanticFallback = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsVectorFallback = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_strategy_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueryIntentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetrievalStrategyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartGraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueryText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RetrievedCount = table.Column<int>(type: "integer", nullable: false),
                    FilteredCount = table.Column<int>(type: "integer", nullable: false),
                    DeniedCount = table.Column<int>(type: "integer", nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retrieval_runs_query_intent_versions_QueryIntentVersionId",
                        column: x => x.QueryIntentVersionId,
                        principalTable: "query_intent_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retrieval_runs_retrieval_strategy_versions_RetrievalStrateg~",
                        column: x => x.RetrievalStrategyVersionId,
                        principalTable: "retrieval_strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetrievalRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PolicyEvaluationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetrievedContextJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    FilteredContextJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    DeniedSummariesJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    SensitiveDeniedReferencesJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    LlmVisibleContextJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    AllowedCount = table.Column<int>(type: "integer", nullable: false),
                    DeniedCount = table.Column<int>(type: "integer", nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_context_packages_retrieval_runs_RetrievalRunId",
                        column: x => x.RetrievalRunId,
                        principalTable: "retrieval_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_access_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ContextType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_access_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_context_access_decisions_context_packages_ContextPackageId",
                        column: x => x.ContextPackageId,
                        principalTable: "context_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_context_access_decisions_ContextPackageId",
                table: "context_access_decisions",
                column: "ContextPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_context_access_decisions_TenantId_ContextPackageId_DisplayO~",
                table: "context_access_decisions",
                columns: new[] { "TenantId", "ContextPackageId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_RetrievalRunId",
                table: "context_packages",
                column: "RetrievalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_TenantId_RetrievalRunId_CreatedAt",
                table: "context_packages",
                columns: new[] { "TenantId", "RetrievalRunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_query_intent_versions_TenantId_NormalizedIntentKey_Normaliz~",
                table: "query_intent_versions",
                columns: new[] { "TenantId", "NormalizedIntentKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_query_intent_versions_TenantId_Source_IsEnabled",
                table: "query_intent_versions",
                columns: new[] { "TenantId", "Source", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_runs_QueryIntentVersionId",
                table: "retrieval_runs",
                column: "QueryIntentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_runs_RetrievalStrategyVersionId",
                table: "retrieval_runs",
                column: "RetrievalStrategyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_runs_TenantId_CreatedAt",
                table: "retrieval_runs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_runs_TenantId_QueryIntentVersionId_CreatedAt",
                table: "retrieval_runs",
                columns: new[] { "TenantId", "QueryIntentVersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_strategy_versions_TenantId_NormalizedStrategyKey_~",
                table: "retrieval_strategy_versions",
                columns: new[] { "TenantId", "NormalizedStrategyKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_strategy_versions_TenantId_Source_IsEnabled",
                table: "retrieval_strategy_versions",
                columns: new[] { "TenantId", "Source", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "context_access_decisions");

            migrationBuilder.DropTable(
                name: "context_packages");

            migrationBuilder.DropTable(
                name: "retrieval_runs");

            migrationBuilder.DropTable(
                name: "query_intent_versions");

            migrationBuilder.DropTable(
                name: "retrieval_strategy_versions");
        }
    }
}
