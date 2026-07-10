using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue29StructuralRelationshipType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedStructuralRelationshipType",
                table: "import_mapping_versions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StructuralRelationshipType",
                table: "import_mapping_versions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedStructuralRelationshipType",
                table: "import_mapping_versions");

            migrationBuilder.DropColumn(
                name: "StructuralRelationshipType",
                table: "import_mapping_versions");
        }
    }
}
