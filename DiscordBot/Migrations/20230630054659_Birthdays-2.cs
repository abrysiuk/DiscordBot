using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class Birthdays2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BirthdayDefs",
                table: "BirthdayDefs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BirthdayDefs",
                table: "BirthdayDefs",
                columns: new[] { "UserId", "GuildId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BirthdayDefs",
                table: "BirthdayDefs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BirthdayDefs",
                table: "BirthdayDefs",
                column: "UserId");
        }
    }
}
