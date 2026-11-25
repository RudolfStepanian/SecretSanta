using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecretSanta.Data.Migrations
{
    /// <inheritdoc />
    public partial class OneTimeResetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResetToken",
                table: "Participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetUsedAt",
                table: "Participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_ResetToken",
                table: "Participants",
                column: "ResetToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_ResetToken",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "ResetToken",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "ResetUsedAt",
                table: "Participants");
        }
    }
}
