using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecretSanta.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParticipantLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_GameId",
                table: "Participants");

            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "Participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_GameId_Login",
                table: "Participants",
                columns: new[] { "GameId", "Login" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_GameId_Login",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "Participants");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_GameId",
                table: "Participants",
                column: "GameId");
        }
    }
}
