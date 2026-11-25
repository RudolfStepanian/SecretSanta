using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecretSanta.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResetHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResetAt",
                table: "Participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResetCount",
                table: "Participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetAt",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "ResetCount",
                table: "Participants");
        }
    }
}
