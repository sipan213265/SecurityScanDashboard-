using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SecurityScanDashboard.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "belek_appsec");

            migrationBuilder.CreateTable(
                name: "Repositories",
                schema: "belek_appsec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LiveUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scans",
                schema: "belek_appsec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RepositoryId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scans_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "belek_appsec",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vulnerabilities",
                schema: "belek_appsec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    CweId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CveId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vulnerabilities_Scans_ScanId",
                        column: x => x.ScanId,
                        principalSchema: "belek_appsec",
                        principalTable: "Scans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_OwnerId",
                schema: "belek_appsec",
                table: "Repositories",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Url",
                schema: "belek_appsec",
                table: "Repositories",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_RepositoryId",
                schema: "belek_appsec",
                table: "Scans",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_StartedAt",
                schema: "belek_appsec",
                table: "Scans",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_Status",
                schema: "belek_appsec",
                table: "Scans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_DetectedAt",
                schema: "belek_appsec",
                table: "Vulnerabilities",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_ScanId",
                schema: "belek_appsec",
                table: "Vulnerabilities",
                column: "ScanId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_Severity",
                schema: "belek_appsec",
                table: "Vulnerabilities",
                column: "Severity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vulnerabilities",
                schema: "belek_appsec");

            migrationBuilder.DropTable(
                name: "Scans",
                schema: "belek_appsec");

            migrationBuilder.DropTable(
                name: "Repositories",
                schema: "belek_appsec");
        }
    }
}
