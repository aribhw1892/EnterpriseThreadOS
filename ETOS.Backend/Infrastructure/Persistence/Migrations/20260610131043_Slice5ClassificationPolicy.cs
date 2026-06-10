using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice5ClassificationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "classification_schemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_schemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_evaluation_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllowedCount = table.Column<int>(type: "integer", nullable: false),
                    DeniedCount = table.Column<int>(type: "integer", nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_evaluation_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "classification_scheme_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LevelsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_scheme_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_classification_scheme_versions_classification_schemes_Schem~",
                        column: x => x.SchemeId,
                        principalTable: "classification_schemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedPolicyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClassificationSchemeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_versions_classification_scheme_versions_Classificati~",
                        column: x => x.ClassificationSchemeVersionId,
                        principalTable: "classification_scheme_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restricted_context_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedClassificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NormalizedAttributeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NormalizedDocumentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RequiredPermissionKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NormalizedRequiredPermissionKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AllowedRoleName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NormalizedAllowedRoleName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RequiresGrant = table.Column<bool>(type: "boolean", nullable: false),
                    Effect = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SafeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restricted_context_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restricted_context_rules_policy_versions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalTable: "policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_classification_scheme_versions_SchemeId_NormalizedVersionLa~",
                table: "classification_scheme_versions",
                columns: new[] { "SchemeId", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_classification_scheme_versions_TenantId_State_PublishedAt",
                table: "classification_scheme_versions",
                columns: new[] { "TenantId", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_classification_schemes_TenantId_NormalizedKey",
                table: "classification_schemes",
                columns: new[] { "TenantId", "NormalizedKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_evaluation_records_TenantId_Action_CreatedAt",
                table: "policy_evaluation_records",
                columns: new[] { "TenantId", "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_evaluation_records_TenantId_PolicyVersionId_CreatedAt",
                table: "policy_evaluation_records",
                columns: new[] { "TenantId", "PolicyVersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_ClassificationSchemeVersionId",
                table: "policy_versions",
                column: "ClassificationSchemeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_TenantId_NormalizedPolicyKey_NormalizedVers~",
                table: "policy_versions",
                columns: new[] { "TenantId", "NormalizedPolicyKey", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_TenantId_NormalizedPolicyKey_State_Publishe~",
                table: "policy_versions",
                columns: new[] { "TenantId", "NormalizedPolicyKey", "State", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_restricted_context_rules_PolicyVersionId",
                table: "restricted_context_rules",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_restricted_context_rules_TenantId_NormalizedClassificationK~",
                table: "restricted_context_rules",
                columns: new[] { "TenantId", "NormalizedClassificationKey", "NormalizedAttributeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_restricted_context_rules_TenantId_PolicyVersionId",
                table: "restricted_context_rules",
                columns: new[] { "TenantId", "PolicyVersionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_evaluation_records");

            migrationBuilder.DropTable(
                name: "restricted_context_rules");

            migrationBuilder.DropTable(
                name: "policy_versions");

            migrationBuilder.DropTable(
                name: "classification_scheme_versions");

            migrationBuilder.DropTable(
                name: "classification_schemes");
        }
    }
}
