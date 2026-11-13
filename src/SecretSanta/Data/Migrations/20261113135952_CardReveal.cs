using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecretSanta.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardReveal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RevealedAt",
                table: "Participants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevealedAt",
                table: "Participants");
        }
    }
}
