using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgiloxSortingHall.Migrations
{
    /// <inheritdoc />
    public partial class ArticleInformationChanged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Article",
                table: "PalletSlots");

            migrationBuilder.AddColumn<string>(
                name: "Article",
                table: "HallRows",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Article",
                table: "HallRows");

            migrationBuilder.AddColumn<string>(
                name: "Article",
                table: "PalletSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
