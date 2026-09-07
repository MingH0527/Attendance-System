using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebAndMobile.Models;

#nullable disable

namespace WebAndMobile.Migrations
{
    [DbContext(typeof(DB))]
    [Migration("20260906213000_AddReplacementOccurrenceLink")]
    public partial class AddReplacementOccurrenceLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "SourceOccurrenceDate", table: "ReplacementSessions", type: "datetime2", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_ReplacementSessions_SourceTimetableId_SourceOccurrenceDate_Status", table: "ReplacementSessions", columns: new[] { "SourceTimetableId", "SourceOccurrenceDate", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ReplacementSessions_SourceTimetableId_SourceOccurrenceDate_Status", table: "ReplacementSessions");
            migrationBuilder.DropColumn(name: "SourceOccurrenceDate", table: "ReplacementSessions");
        }
    }
}
