using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discordGuild",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discordGuild", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "userMessages",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    IsTTS = table.Column<bool>(type: "bit", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsSuppressed = table.Column<bool>(type: "bit", nullable: false),
                    MentionedEveryone = table.Column<bool>(type: "bit", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CleanContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditedTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ChannelMention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    AuthorId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    AuthorMention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThreadID = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    ThreadMention = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "discordMessageChannel",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discordMessageChannel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_discordMessageChannel_discordGuild_GuildId",
                        column: x => x.GuildId,
                        principalTable: "discordGuild",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "discordShame",
                columns: table => new
                {
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessageId = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discordShame", x => new { x.Type, x.MessageId });
                    table.ForeignKey(
                        name: "FK_discordShame_userMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "userMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "slashLogs",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Command = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorID = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    AuthorMention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ThreadId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slashLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_slashLogs_discordMessageChannel_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "discordMessageChannel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_discordMessageChannel_GuildId",
                table: "discordMessageChannel",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_discordShame_MessageId",
                table: "discordShame",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_slashLogs_ChannelId",
                table: "slashLogs",
                column: "ChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discordShame");

            migrationBuilder.DropTable(
                name: "slashLogs");

            migrationBuilder.DropTable(
                name: "userMessages");

            migrationBuilder.DropTable(
                name: "discordMessageChannel");

            migrationBuilder.DropTable(
                name: "discordGuild");
        }
    }
}
