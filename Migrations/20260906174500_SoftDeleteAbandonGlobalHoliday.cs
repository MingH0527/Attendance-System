using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WebAndMobile.Models;

#nullable disable

namespace WebAndMobile.Migrations
{
    [DbContext(typeof(DB))]
    [Migration("20260906174500_SoftDeleteAbandonGlobalHoliday")]
    public partial class SoftDeleteAbandonGlobalHoliday : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicCalendarEvents_AcademicTerms_TermId",
                table: "AcademicCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "TermId",
                table: "AcademicCalendarEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // Convert existing semester-specific holidays into one central calendar.
            // Keep one copy of each same-date/same-name holiday before adding the global unique index.
            migrationBuilder.Sql(@"
WITH HolidayDuplicates AS (
    SELECT CalendarEventId,
           ROW_NUMBER() OVER (PARTITION BY EventDate, Name ORDER BY CalendarEventId) AS rn
    FROM AcademicCalendarEvents
)
DELETE FROM AcademicCalendarEvents
WHERE CalendarEventId IN (SELECT CalendarEventId FROM HolidayDuplicates WHERE rn > 1);

UPDATE AcademicCalendarEvents SET TermId = NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_EventDate_Name",
                table: "AcademicCalendarEvents",
                columns: new[] { "EventDate", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicCalendarEvents_AcademicTerms_TermId",
                table: "AcademicCalendarEvents",
                column: "TermId",
                principalTable: "AcademicTerms",
                principalColumn: "TermId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicCalendarEvents_AcademicTerms_TermId",
                table: "AcademicCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_AcademicCalendarEvents_EventDate_Name",
                table: "AcademicCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "TermId",
                table: "AcademicCalendarEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicCalendarEvents_AcademicTerms_TermId",
                table: "AcademicCalendarEvents",
                column: "TermId",
                principalTable: "AcademicTerms",
                principalColumn: "TermId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
