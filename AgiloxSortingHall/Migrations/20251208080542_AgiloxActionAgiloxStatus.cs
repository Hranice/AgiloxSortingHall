using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgiloxSortingHall.Migrations
{
    /// <inheritdoc />
    public partial class AgiloxActionAgiloxStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderStatus",
                table: "RowCalls");

            migrationBuilder.RenameColumn(
                name: "LastAgiloxEvent",
                table: "RowCalls",
                newName: "LastAgiloxStatus");

            migrationBuilder.AddColumn<string>(
                name: "LastAgiloxAction",
                table: "RowCalls",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAgiloxAction",
                table: "RowCalls");

            migrationBuilder.RenameColumn(
                name: "LastAgiloxStatus",
                table: "RowCalls",
                newName: "LastAgiloxEvent");

            migrationBuilder.AddColumn<int>(
                name: "OrderStatus",
                table: "RowCalls",
                type: "INTEGER",
                nullable: true);
        }
    }
}
