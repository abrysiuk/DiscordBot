using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class Grammar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrammarRule",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Urls = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarRule", x => new { x.Id, x.SubId });
                });

            migrationBuilder.CreateTable(
                name: "GrammarMatchs",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscordMessageId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShortMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Offset = table.Column<int>(type: "int", nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    Replacements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sentence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RuleSubId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarMatchs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarMatchs_GrammarRule_RuleId_RuleSubId",
                        columns: x => new { x.RuleId, x.RuleSubId },
                        principalTable: "GrammarRule",
                        principalColumns: new[] { "Id", "SubId" });
                    table.ForeignKey(
                        name: "FK_GrammarMatchs_UserMessages_DiscordMessageId",
                        column: x => x.DiscordMessageId,
                        principalTable: "UserMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarMatchs_DiscordMessageId",
                table: "GrammarMatchs",
                column: "DiscordMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarMatchs_RuleId_RuleSubId",
                table: "GrammarMatchs",
                columns: new[] { "RuleId", "RuleSubId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrammarMatchs");

            migrationBuilder.DropTable(
                name: "GrammarRule");
        }
    }
}
