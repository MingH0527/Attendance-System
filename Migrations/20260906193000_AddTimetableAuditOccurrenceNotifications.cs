using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebAndMobile.Models;

#nullable disable

namespace WebAndMobile.Migrations
{
    [DbContext(typeof(DB))]
    [Migration("20260906193000_AddTimetableAuditOccurrenceNotifications")]
    public partial class AddTimetableAuditOccurrenceNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "OverrideClassroomId", table: "TimetableExceptions", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<TimeSpan>(name: "OverrideEndTime", table: "TimetableExceptions", type: "time", nullable: true);
            migrationBuilder.AddColumn<TimeSpan>(name: "OverrideStartTime", table: "TimetableExceptions", type: "time", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ResolvedAt", table: "TimetableExceptions", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ResolvedBy", table: "TimetableExceptions", type: "nvarchar(20)", maxLength: 20, nullable: true);

            migrationBuilder.AddColumn<DateTime>(name: "OccurrenceDate", table: "TimetableNotifications", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "UserId", table: "TimetableNotifications", type: "nvarchar(20)", maxLength: 20, nullable: true);

            migrationBuilder.CreateTable(
                name: "TimetableAudits",
                columns: table => new
                {
                    AuditId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimetableId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeforeValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AfterValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_TimetableAudits", x => x.AuditId));

            migrationBuilder.CreateIndex(name: "IX_TimetableAudits_TimetableId_OccurrenceDate_Status", table: "TimetableAudits", columns: new[] { "TimetableId", "OccurrenceDate", "Status" });
            migrationBuilder.CreateIndex(name: "IX_TimetableNotifications_UserId_IsRead_CreatedAt", table: "TimetableNotifications", columns: new[] { "UserId", "IsRead", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TimetableAudits");
            migrationBuilder.DropIndex(name: "IX_TimetableNotifications_UserId_IsRead_CreatedAt", table: "TimetableNotifications");
            migrationBuilder.DropColumn(name: "OccurrenceDate", table: "TimetableNotifications");
            migrationBuilder.DropColumn(name: "UserId", table: "TimetableNotifications");
            migrationBuilder.DropColumn(name: "OverrideClassroomId", table: "TimetableExceptions");
            migrationBuilder.DropColumn(name: "OverrideEndTime", table: "TimetableExceptions");
            migrationBuilder.DropColumn(name: "OverrideStartTime", table: "TimetableExceptions");
            migrationBuilder.DropColumn(name: "ResolvedAt", table: "TimetableExceptions");
            migrationBuilder.DropColumn(name: "ResolvedBy", table: "TimetableExceptions");
        }
    }
}
