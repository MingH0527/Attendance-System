using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyCodeAndDeviceLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasskeyCode",
                table: "UserPasskeys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeys_PasskeyCode",
                table: "UserPasskeys",
                column: "PasskeyCode",
                unique: true,
                filter: "[PasskeyCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPasskeys_PasskeyCode",
                table: "UserPasskeys");

            migrationBuilder.DropColumn(
                name: "PasskeyCode",
                table: "UserPasskeys");
        }
    }
}
