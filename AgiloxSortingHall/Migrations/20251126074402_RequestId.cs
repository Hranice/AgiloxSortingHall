using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgiloxSortingHall.Migrations
{
    /// <inheritdoc />
    public partial class RequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgiloxRequestId",
                table: "RowCalls");

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "RowCalls",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "RowCalls");

            migrationBuilder.AddColumn<string>(
                name: "AgiloxRequestId",
                table: "RowCalls",
                type: "TEXT",
                nullable: true);
        }
    }
}
