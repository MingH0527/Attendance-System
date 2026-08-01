using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramIdToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Classes_AcademicPrograms_ProgramId",
                table: "Classes",
                column: "ProgramId",
                principalTable: "AcademicPrograms",
                principalColumn: "ProgramId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_AcademicPrograms_ProgramId",
                table: "Classes");
        }
    }
}
