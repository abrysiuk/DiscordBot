using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class ShameDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_discordMessageChannel_discordGuild_GuildId",
                table: "discordMessageChannel");

            migrationBuilder.DropForeignKey(
                name: "FK_discordShame_userMessages_MessageId",
                table: "discordShame");

            migrationBuilder.DropForeignKey(
                name: "FK_slashLogs_discordMessageChannel_ChannelId",
                table: "slashLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_userMessages",
                table: "userMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_slashLogs",
                table: "slashLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_discordShame",
                table: "discordShame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_discordMessageChannel",
                table: "discordMessageChannel");

            migrationBuilder.DropPrimaryKey(
                name: "PK_discordGuild",
                table: "discordGuild");

            migrationBuilder.RenameTable(
                name: "userMessages",
                newName: "UserMessages");

            migrationBuilder.RenameTable(
                name: "slashLogs",
                newName: "SlashLogs");

            migrationBuilder.RenameTable(
                name: "discordShame",
                newName: "DiscordShame");

            migrationBuilder.RenameTable(
                name: "discordMessageChannel",
                newName: "DiscordMessageChannel");

            migrationBuilder.RenameTable(
                name: "discordGuild",
                newName: "DiscordGuild");

            migrationBuilder.RenameIndex(
                name: "IX_slashLogs_ChannelId",
                table: "SlashLogs",
                newName: "IX_SlashLogs_ChannelId");

            migrationBuilder.RenameIndex(
                name: "IX_discordShame_MessageId",
                table: "DiscordShame",
                newName: "IX_DiscordShame_MessageId");

            migrationBuilder.RenameIndex(
                name: "IX_discordMessageChannel_GuildId",
                table: "DiscordMessageChannel",
                newName: "IX_DiscordMessageChannel_GuildId");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Date",
                table: "DiscordShame",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserMessages",
                table: "UserMessages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SlashLogs",
                table: "SlashLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscordShame",
                table: "DiscordShame",
                columns: new[] { "Type", "MessageId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscordMessageChannel",
                table: "DiscordMessageChannel",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscordGuild",
                table: "DiscordGuild",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscordMessageChannel_DiscordGuild_GuildId",
                table: "DiscordMessageChannel",
                column: "GuildId",
                principalTable: "DiscordGuild",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscordShame_UserMessages_MessageId",
                table: "DiscordShame",
                column: "MessageId",
                principalTable: "UserMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SlashLogs_DiscordMessageChannel_ChannelId",
                table: "SlashLogs",
                column: "ChannelId",
                principalTable: "DiscordMessageChannel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscordMessageChannel_DiscordGuild_GuildId",
                table: "DiscordMessageChannel");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscordShame_UserMessages_MessageId",
                table: "DiscordShame");

            migrationBuilder.DropForeignKey(
                name: "FK_SlashLogs_DiscordMessageChannel_ChannelId",
                table: "SlashLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserMessages",
                table: "UserMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SlashLogs",
                table: "SlashLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscordShame",
                table: "DiscordShame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscordMessageChannel",
                table: "DiscordMessageChannel");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscordGuild",
                table: "DiscordGuild");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "DiscordShame");

            migrationBuilder.RenameTable(
                name: "UserMessages",
                newName: "userMessages");

            migrationBuilder.RenameTable(
                name: "SlashLogs",
                newName: "slashLogs");

            migrationBuilder.RenameTable(
                name: "DiscordShame",
                newName: "discordShame");

            migrationBuilder.RenameTable(
                name: "DiscordMessageChannel",
                newName: "discordMessageChannel");

            migrationBuilder.RenameTable(
                name: "DiscordGuild",
                newName: "discordGuild");

            migrationBuilder.RenameIndex(
                name: "IX_SlashLogs_ChannelId",
                table: "slashLogs",
                newName: "IX_slashLogs_ChannelId");

            migrationBuilder.RenameIndex(
                name: "IX_DiscordShame_MessageId",
                table: "discordShame",
                newName: "IX_discordShame_MessageId");

            migrationBuilder.RenameIndex(
                name: "IX_DiscordMessageChannel_GuildId",
                table: "discordMessageChannel",
                newName: "IX_discordMessageChannel_GuildId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_userMessages",
                table: "userMessages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_slashLogs",
                table: "slashLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_discordShame",
                table: "discordShame",
                columns: new[] { "Type", "MessageId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_discordMessageChannel",
                table: "discordMessageChannel",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_discordGuild",
                table: "discordGuild",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_discordMessageChannel_discordGuild_GuildId",
                table: "discordMessageChannel",
                column: "GuildId",
                principalTable: "discordGuild",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_discordShame_userMessages_MessageId",
                table: "discordShame",
                column: "MessageId",
                principalTable: "userMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_slashLogs_discordMessageChannel_ChannelId",
                table: "slashLogs",
                column: "ChannelId",
                principalTable: "discordMessageChannel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
