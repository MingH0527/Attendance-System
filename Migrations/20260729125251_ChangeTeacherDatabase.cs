using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTeacherDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Teacher");

            migrationBuilder.AddColumn<string>(
                name: "Program",
                table: "Teacher",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Teacher",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Program",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Teacher");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Teacher",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
