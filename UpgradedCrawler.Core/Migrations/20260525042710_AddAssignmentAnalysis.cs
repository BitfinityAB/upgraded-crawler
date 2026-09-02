using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpgradedCrawler.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssignmentId = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MatchScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchReason = table.Column<string>(type: "TEXT", nullable: false),
                    ColdEmailDraft = table.Column<string>(type: "TEXT", nullable: false),
                    CoverLetterDraft = table.Column<string>(type: "TEXT", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentAnalyses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentAnalyses_AssignmentId_ProviderId",
                table: "AssignmentAnalyses",
                columns: new[] { "AssignmentId", "ProviderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentAnalyses");
        }
    }
}
