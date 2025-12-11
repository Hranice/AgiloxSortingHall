using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgiloxSortingHall.Migrations
{
    /// <inheritdoc />
    public partial class AdnULLABLEhALLrOWiD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RowCalls_HallRows_HallRowId",
                table: "RowCalls");

            migrationBuilder.AlterColumn<int>(
                name: "HallRowId",
                table: "RowCalls",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_RowCalls_HallRows_HallRowId",
                table: "RowCalls",
                column: "HallRowId",
                principalTable: "HallRows",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RowCalls_HallRows_HallRowId",
                table: "RowCalls");

            migrationBuilder.AlterColumn<int>(
                name: "HallRowId",
                table: "RowCalls",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RowCalls_HallRows_HallRowId",
                table: "RowCalls",
                column: "HallRowId",
                principalTable: "HallRows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
