using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyDeviceLimitsAndEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPasskeys_UserId",
                table: "UserPasskeys");

            migrationBuilder.AddColumn<string>(
                name: "DeviceIdentifier",
                table: "UserPasskeys",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelyingPartyId",
                table: "UserPasskeys",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeys_DeviceIdentifier",
                table: "UserPasskeys",
                column: "DeviceIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeys_UserId_DeviceIdentifier",
                table: "UserPasskeys",
                columns: new[] { "UserId", "DeviceIdentifier" },
                unique: true,
                filter: "[DeviceIdentifier] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPasskeys_DeviceIdentifier",
                table: "UserPasskeys");

            migrationBuilder.DropIndex(
                name: "IX_UserPasskeys_UserId_DeviceIdentifier",
                table: "UserPasskeys");

            migrationBuilder.DropColumn(
                name: "DeviceIdentifier",
                table: "UserPasskeys");

            migrationBuilder.DropColumn(
                name: "RelyingPartyId",
                table: "UserPasskeys");

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeys_UserId",
                table: "UserPasskeys",
                column: "UserId");
        }
    }
}
