using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecretSanta.Data.Migrations
{
    /// <inheritdoc />
    public partial class MailLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Participants",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "Participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailToken",
                table: "Participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "Participants",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetSentAt",
                table: "Participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EmailToken",
                table: "Participants",
                column: "EmailToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_EmailToken",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "EmailToken",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "ResetSentAt",
                table: "Participants");
        }
    }
}
