using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETOS.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice4ArtifactRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedArtifactType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifacts_identity_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artifact_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifact_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifact_relationships_artifacts_SourceArtifactId",
                        column: x => x.SourceArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_relationships_artifacts_TargetArtifactId",
                        column: x => x.TargetArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artifact_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedVersionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PayloadJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ReadinessState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CompatibilityStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CompatibilitySummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PolicyRiskStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifact_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifact_versions_artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_versions_identity_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_versions_identity_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artifact_dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependencyKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifact_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifact_dependencies_artifact_versions_DependentVersionId",
                        column: x => x.DependentVersionId,
                        principalTable: "artifact_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_dependencies_artifact_versions_RequiredVersionId",
                        column: x => x.RequiredVersionId,
                        principalTable: "artifact_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_dependencies_artifacts_RequiredArtifactId",
                        column: x => x.RequiredArtifactId,
                        principalTable: "artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artifact_dependencies_DependentVersionId",
                table: "artifact_dependencies",
                column: "DependentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_dependencies_RequiredArtifactId",
                table: "artifact_dependencies",
                column: "RequiredArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_dependencies_RequiredVersionId",
                table: "artifact_dependencies",
                column: "RequiredVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_dependencies_TenantId_DependentVersionId_RequiredV~",
                table: "artifact_dependencies",
                columns: new[] { "TenantId", "DependentVersionId", "RequiredVersionId", "DependencyKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artifact_relationships_SourceArtifactId",
                table: "artifact_relationships",
                column: "SourceArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_relationships_TargetArtifactId",
                table: "artifact_relationships",
                column: "TargetArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_relationships_TenantId_SourceArtifactId_TargetArti~",
                table: "artifact_relationships",
                columns: new[] { "TenantId", "SourceArtifactId", "TargetArtifactId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artifact_versions_ArtifactId_CreatedAt",
                table: "artifact_versions",
                columns: new[] { "ArtifactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_artifact_versions_ArtifactId_NormalizedVersionLabel",
                table: "artifact_versions",
                columns: new[] { "ArtifactId", "NormalizedVersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artifact_versions_CreatedByUserId",
                table: "artifact_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_versions_PublishedByUserId",
                table: "artifact_versions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_OwnerUserId",
                table: "artifacts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_TenantId_NormalizedArtifactType",
                table: "artifacts",
                columns: new[] { "TenantId", "NormalizedArtifactType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifact_dependencies");

            migrationBuilder.DropTable(
                name: "artifact_relationships");

            migrationBuilder.DropTable(
                name: "artifact_versions");

            migrationBuilder.DropTable(
                name: "artifacts");
        }
    }
}
