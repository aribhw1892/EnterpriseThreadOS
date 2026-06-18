using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue181IndustryNeutralPackageProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportProfileJson",
                table: "model_package_versions",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueryIntentExtensionsJson",
                table: "model_package_versions",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_mapping_learning_signal_inputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportMappingVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DiffJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    AutonomousRetraining = table.Column<bool>(type: "boolean", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_mapping_learning_signal_inputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_mapping_learning_signal_inputs_import_mapping_versio~",
                        column: x => x.ImportMappingVersionId,
                        principalTable: "import_mapping_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_mapping_learning_signal_inputs_ImportMappingVersionId",
                table: "import_mapping_learning_signal_inputs",
                column: "ImportMappingVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_import_mapping_learning_signal_inputs_TenantId_ImportMappin~",
                table: "import_mapping_learning_signal_inputs",
                columns: new[] { "TenantId", "ImportMappingVersionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_mapping_learning_signal_inputs");

            migrationBuilder.DropColumn(
                name: "ImportProfileJson",
                table: "model_package_versions");

            migrationBuilder.DropColumn(
                name: "QueryIntentExtensionsJson",
                table: "model_package_versions");
        }
    }
}
