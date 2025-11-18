using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgiloxSortingHall.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PalletSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HallRowId = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Article = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PalletSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PalletSlots_HallRows_HallRowId",
                        column: x => x.HallRowId,
                        principalTable: "HallRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RowCalls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkTableId = table.Column<int>(type: "INTEGER", nullable: false),
                    HallRowId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RowCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RowCalls_HallRows_HallRowId",
                        column: x => x.HallRowId,
                        principalTable: "HallRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RowCalls_WorkTables_WorkTableId",
                        column: x => x.WorkTableId,
                        principalTable: "WorkTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PalletSlots_HallRowId_PositionIndex",
                table: "PalletSlots",
                columns: new[] { "HallRowId", "PositionIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RowCalls_HallRowId",
                table: "RowCalls",
                column: "HallRowId");

            migrationBuilder.CreateIndex(
                name: "IX_RowCalls_WorkTableId",
                table: "RowCalls",
                column: "WorkTableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PalletSlots");

            migrationBuilder.DropTable(
                name: "RowCalls");

            migrationBuilder.DropTable(
                name: "HallRows");

            migrationBuilder.DropTable(
                name: "WorkTables");
        }
    }
}
