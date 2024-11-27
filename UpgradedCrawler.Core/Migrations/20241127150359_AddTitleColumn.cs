using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpgradedCrawler.Core.Migrations
{
    /// <inheritdoc />

    public partial class AddTitleColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Assignments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Assignments");
        }
    }
}

