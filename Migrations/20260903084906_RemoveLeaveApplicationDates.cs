using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLeaveApplicationDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveApplicationDates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveApplicationDates",
                columns: table => new
                {
                    LeaveDateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveId = table.Column<int>(type: "int", nullable: false),
                    LeaveDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveApplicationDates", x => x.LeaveDateId);
                    table.ForeignKey(
                        name: "FK_LeaveApplicationDates_LeaveApplications_LeaveId",
                        column: x => x.LeaveId,
                        principalTable: "LeaveApplications",
                        principalColumn: "LeaveId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplicationDates_LeaveId",
                table: "LeaveApplicationDates",
                column: "LeaveId");
        }
    }
}
