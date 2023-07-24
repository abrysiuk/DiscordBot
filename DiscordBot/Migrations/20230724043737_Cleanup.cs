using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class Cleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuantityParse_UserMessages_MessageId",
                table: "QuantityParse");

            migrationBuilder.AlterColumn<decimal>(
                name: "MessageId",
                table: "QuantityParse",
                type: "decimal(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,0)");

            migrationBuilder.AddForeignKey(
                name: "FK_QuantityParse_UserMessages_MessageId",
                table: "QuantityParse",
                column: "MessageId",
                principalTable: "UserMessages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuantityParse_UserMessages_MessageId",
                table: "QuantityParse");

            migrationBuilder.AlterColumn<decimal>(
                name: "MessageId",
                table: "QuantityParse",
                type: "decimal(20,0)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,0)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantityParse_UserMessages_MessageId",
                table: "QuantityParse",
                column: "MessageId",
                principalTable: "UserMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
