using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyPhoneDeviceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasskeyDeviceBindings",
                columns: table => new
                {
                    PasskeyDeviceBindingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BindingToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasskeyDeviceBindings", x => x.PasskeyDeviceBindingId);
                    table.ForeignKey(
                        name: "FK_PasskeyDeviceBindings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyDeviceBindings_BindingToken",
                table: "PasskeyDeviceBindings",
                column: "BindingToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyDeviceBindings_DeviceIdentifier_Status",
                table: "PasskeyDeviceBindings",
                columns: new[] { "DeviceIdentifier", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyDeviceBindings_UserId",
                table: "PasskeyDeviceBindings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasskeyDeviceBindings");
        }
    }
}
