using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpgradedCrawler.Core.Migrations
{
    /// <inheritdoc />
    public partial class RefactorIdToAssignmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a new temporary table with the correct schema
            migrationBuilder.Sql(@"
                CREATE TABLE Assignments_New (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AssignmentId TEXT NOT NULL,
                    Url TEXT NOT NULL,
                    ProviderId TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
            ");

            // Copy data from old table to new table (old Id becomes AssignmentId)
            migrationBuilder.Sql(@"
                INSERT INTO Assignments_New (AssignmentId, Url, ProviderId, Title, CreatedAt)
                SELECT Id, Url, ProviderId, Title, CreatedAt
                FROM Assignments;
            ");

            // Drop the old table
            migrationBuilder.DropTable(name: "Assignments");

            // Rename the new table
            migrationBuilder.RenameTable(
                name: "Assignments_New",
                newName: "Assignments");

            // Create the unique index on AssignmentId + ProviderId
            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignmentId_ProviderId",
                table: "Assignments",
                columns: new[] { "AssignmentId", "ProviderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Create old schema table
            migrationBuilder.Sql(@"
                CREATE TABLE Assignments_Old (
                    Id TEXT PRIMARY KEY,
                    Url TEXT NOT NULL,
                    ProviderId TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
            ");

            // Copy data back (AssignmentId becomes Id)
            migrationBuilder.Sql(@"
                INSERT INTO Assignments_Old (Id, Url, ProviderId, Title, CreatedAt)
                SELECT AssignmentId, Url, ProviderId, Title, CreatedAt
                FROM Assignments;
            ");

            // Drop the new table
            migrationBuilder.DropTable(name: "Assignments");

            // Rename back
            migrationBuilder.RenameTable(
                name: "Assignments_Old",
                newName: "Assignments");
        }
    }
}
