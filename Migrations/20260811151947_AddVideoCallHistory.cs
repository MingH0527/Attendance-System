using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAndMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoCallHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CallAnsweredAtUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CallDurationSeconds",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CallEndedAtUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CallStartedAtUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallStatus",
                table: "ChatMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "ChatMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallAnsweredAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CallDurationSeconds",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CallEndedAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CallStartedAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CallStatus",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "ChatMessages");
        }
    }
}
