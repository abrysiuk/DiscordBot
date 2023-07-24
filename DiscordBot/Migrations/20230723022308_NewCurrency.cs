using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class NewCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyConversions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "GuildUsers");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "GuildUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    CurrencyCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Suffix = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rate = table.Column<float>(type: "real", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.CurrencyCode);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildUsers_CurrencyCode",
                table: "GuildUsers",
                column: "CurrencyCode");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUsers_Currencies_CurrencyCode",
                table: "GuildUsers",
                column: "CurrencyCode",
                principalTable: "Currencies",
                principalColumn: "CurrencyCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildUsers_Currencies_CurrencyCode",
                table: "GuildUsers");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_GuildUsers_CurrencyCode",
                table: "GuildUsers");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "GuildUsers");

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "GuildUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CurrencyConversions",
                columns: table => new
                {
                    FromCurrency = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ToCurrency = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rate = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyConversions", x => new { x.FromCurrency, x.ToCurrency });
                });
        }
    }
}
