using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramSubjectHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramSubjectHistories",
                columns: table => new
                {
                    HistoryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProgramId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CourseId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramSubjectHistories", x => x.HistoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSubjectHistories_CreatedAt",
                table: "ProgramSubjectHistories",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramSubjectHistories");
        }
    }
}
