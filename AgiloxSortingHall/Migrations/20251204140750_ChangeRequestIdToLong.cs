using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgiloxSortingHall.Migrations
{
    /// <inheritdoc />
    public partial class ChangeRequestIdToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "RowCalls");

            migrationBuilder.AddColumn<long>(
                name: "OrderId",
                table: "RowCalls",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "RowCalls");

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "RowCalls",
                type: "TEXT",
                nullable: true);
        }
    }
}
