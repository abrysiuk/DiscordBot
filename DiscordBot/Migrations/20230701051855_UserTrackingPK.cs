using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class UserTrackingPK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GuildUsers",
                table: "GuildUsers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GuildUsers",
                table: "GuildUsers",
                columns: new[] { "Id", "GuildId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GuildUsers",
                table: "GuildUsers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GuildUsers",
                table: "GuildUsers",
                column: "Id");
        }
    }
}
