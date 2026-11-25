using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecretSanta.Data.Migrations
{
    /// <inheritdoc />
    public partial class PasswordLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLogins",
                table: "Participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "Participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Participants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLogins",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Participants");
        }
    }
}
