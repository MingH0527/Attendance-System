using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class TimetableV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TermId",
                table: "Timetables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                columns: table => new
                {
                    TermId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicTerms", x => x.TermId);
                });

            migrationBuilder.CreateTable(
                name: "ClassSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimetableId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReplacementId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SessionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_ClassSessions_Timetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "Timetables",
                        principalColumn: "TimetableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramCourses",
                columns: table => new
                {
                    ProgramCourseId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProgramId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CourseId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequiredTeachingHours = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramCourses", x => x.ProgramCourseId);
                    table.ForeignKey(
                        name: "FK_ProgramCourses_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "ProgramId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "CourseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplacementSessions",
                columns: table => new
                {
                    ReplacementId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceTimetableId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReplacementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    TargetClassType = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClassroomId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplacementSessions", x => x.ReplacementId);
                    table.ForeignKey(
                        name: "FK_ReplacementSessions_Timetables_SourceTimetableId",
                        column: x => x.SourceTimetableId,
                        principalTable: "Timetables",
                        principalColumn: "TimetableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableExceptions",
                columns: table => new
                {
                    ExceptionId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimetableId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableExceptions", x => x.ExceptionId);
                    table.ForeignKey(
                        name: "FK_TimetableExceptions_Timetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "Timetables",
                        principalColumn: "TimetableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimetableNotifications",
                columns: table => new
                {
                    NotificationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TermId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimetableId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableNotifications", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "AcademicCalendarEvents",
                columns: table => new
                {
                    CalendarEventId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AffectsClasses = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicCalendarEvents", x => x.CalendarEventId);
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "TermId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcademicWeeks",
                columns: table => new
                {
                    AcademicWeekId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WeekNumber = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeekType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicWeeks", x => x.AcademicWeekId);
                    table.ForeignKey(
                        name: "FK_AcademicWeeks_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "TermId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_TermId",
                table: "Timetables",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_TermId",
                table: "AcademicCalendarEvents",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicWeeks_TermId",
                table: "AcademicWeeks",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_TimetableId",
                table: "ClassSessions",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCourses_CourseId",
                table: "ProgramCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCourses_ProgramId_CourseId",
                table: "ProgramCourses",
                columns: new[] { "ProgramId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReplacementSessions_SourceTimetableId",
                table: "ReplacementSessions",
                column: "SourceTimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableExceptions_TimetableId",
                table: "TimetableExceptions",
                column: "TimetableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Timetables_AcademicTerms_TermId",
                table: "Timetables",
                column: "TermId",
                principalTable: "AcademicTerms",
                principalColumn: "TermId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Timetables_AcademicTerms_TermId",
                table: "Timetables");

            migrationBuilder.DropTable(
                name: "AcademicCalendarEvents");

            migrationBuilder.DropTable(
                name: "AcademicWeeks");

            migrationBuilder.DropTable(
                name: "ClassSessions");

            migrationBuilder.DropTable(
                name: "ProgramCourses");

            migrationBuilder.DropTable(
                name: "ReplacementSessions");

            migrationBuilder.DropTable(
                name: "TimetableExceptions");

            migrationBuilder.DropTable(
                name: "TimetableNotifications");

            migrationBuilder.DropTable(
                name: "AcademicTerms");

            migrationBuilder.DropIndex(
                name: "IX_Timetables_TermId",
                table: "Timetables");

            migrationBuilder.DropColumn(
                name: "TermId",
                table: "Timetables");
        }
    }
}
