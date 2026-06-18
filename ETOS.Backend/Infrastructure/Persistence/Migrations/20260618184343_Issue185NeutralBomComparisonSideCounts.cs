using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issue185NeutralBomComparisonSideCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MissingInEbomCount",
                table: "bom_comparison_runs",
                newName: "MissingInSecondarySideCount");

            migrationBuilder.RenameColumn(
                name: "MissingInCadCount",
                table: "bom_comparison_runs",
                newName: "MissingInPrimarySideCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MissingInSecondarySideCount",
                table: "bom_comparison_runs",
                newName: "MissingInEbomCount");

            migrationBuilder.RenameColumn(
                name: "MissingInPrimarySideCount",
                table: "bom_comparison_runs",
                newName: "MissingInCadCount");
        }
    }
}
