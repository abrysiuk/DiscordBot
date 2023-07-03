using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class ShameRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscordShame_UserMessages_MessageId",
                table: "DiscordShame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscordShame",
                table: "DiscordShame");

            migrationBuilder.RenameTable(
                name: "DiscordShame",
                newName: "DiscordLog");

            migrationBuilder.RenameIndex(
                name: "IX_DiscordShame_MessageId",
                table: "DiscordLog",
                newName: "IX_DiscordLog_MessageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscordLog",
                table: "DiscordLog",
                columns: new[] { "Type", "MessageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiscordLog_UserMessages_MessageId",
                table: "DiscordLog",
                column: "MessageId",
                principalTable: "UserMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscordLog_UserMessages_MessageId",
                table: "DiscordLog");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscordLog",
                table: "DiscordLog");

            migrationBuilder.RenameTable(
                name: "DiscordLog",
                newName: "DiscordShame");

            migrationBuilder.RenameIndex(
                name: "IX_DiscordLog_MessageId",
                table: "DiscordShame",
                newName: "IX_DiscordShame_MessageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscordShame",
                table: "DiscordShame",
                columns: new[] { "Type", "MessageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiscordShame_UserMessages_MessageId",
                table: "DiscordShame",
                column: "MessageId",
                principalTable: "UserMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
