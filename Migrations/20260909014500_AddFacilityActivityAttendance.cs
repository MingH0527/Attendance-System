using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebAndMobile.Models;

#nullable disable

namespace WebAndMobile.Migrations
{
    [DbContext(typeof(DB))]
    [Migration("20260909014500_AddFacilityActivityAttendance")]
    public partial class AddFacilityActivityAttendance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    FacilityId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FacilityName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Facilities", x => x.FacilityId); });

            migrationBuilder.CreateTable(
                name: "FacilityImages",
                columns: table => new
                {
                    FacilityImageId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsCover = table.Column<bool>(type: "bit", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityImages", x => x.FacilityImageId);
                    table.ForeignKey("FK_FacilityImages_Facilities_FacilityId", x => x.FacilityId, "Facilities", "FacilityId", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilityBookings",
                columns: table => new
                {
                    BookingId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BookingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartHour = table.Column<int>(type: "int", nullable: false),
                    EndHour = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    QrToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityBookings", x => x.BookingId);
                    table.ForeignKey("FK_FacilityBookings_Facilities_FacilityId", x => x.FacilityId, "Facilities", "FacilityId", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_FacilityBookings_Users_UserId", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilityAttendances",
                columns: table => new
                {
                    FacilityAttendanceId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QrVerified = table.Column<bool>(type: "bit", nullable: false),
                    FaceVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerificationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityAttendances", x => x.FacilityAttendanceId);
                    table.ForeignKey("FK_FacilityAttendances_Facilities_FacilityId", x => x.FacilityId, "Facilities", "FacilityId", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_FacilityAttendances_FacilityBookings_BookingId", x => x.BookingId, "FacilityBookings", "BookingId", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_FacilityAttendances_Users_UserId", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
    INSERT INTO Facilities
        (FacilityId, FacilityName, Location, Capacity, Description)
    VALUES
        ('FAC001', 'Computer Lab', 'Block A', 30,
         'A 24-hour self-service computer laboratory for student activities and independent work.'),

        ('FAC002', 'Gym', 'Sports Centre', 30,
         'A 24-hour self-service gym for student fitness and physical activities.'),

        ('FAC003', 'Swimming Pool', 'Pool Area', 40,
         'A 24-hour self-service swimming facility for approved student activities.'),

        ('FAC004', 'Sports Court', 'Sports Centre', 40,
         'A 24-hour self-service sports court for student recreational activities.'),

        ('FAC005', 'Music Room', 'Block B', 20,
         'A 24-hour self-service music room for student practice and activities.');
");

            migrationBuilder.CreateIndex("IX_FacilityImages_FacilityId", "FacilityImages", "FacilityId");
            migrationBuilder.CreateIndex("IX_FacilityBookings_FacilityId_BookingDate_Status", "FacilityBookings", new[] { "FacilityId", "BookingDate", "Status" });
            migrationBuilder.CreateIndex("IX_FacilityBookings_QrToken", "FacilityBookings", "QrToken", unique: true, filter: "[QrToken] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_FacilityBookings_UserId", "FacilityBookings", "UserId");
            migrationBuilder.CreateIndex("IX_FacilityAttendances_BookingId", "FacilityAttendances", "BookingId", unique: true);
            migrationBuilder.CreateIndex("IX_FacilityAttendances_FacilityId", "FacilityAttendances", "FacilityId");
            migrationBuilder.CreateIndex("IX_FacilityAttendances_UserId", "FacilityAttendances", "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("FacilityAttendances");
            migrationBuilder.DropTable("FacilityImages");
            migrationBuilder.DropTable("FacilityBookings");
            migrationBuilder.DropTable("Facilities");
        }
    }
}
