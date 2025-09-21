using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReceptionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockReceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ReceptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProductMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockReceptionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockReceptionId = table.Column<int>(type: "int", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceptionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceptionItems_StockReceptions_StockReceptionId",
                        column: x => x.StockReceptionId,
                        principalTable: "StockReceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceptionItems_StockReceptionId",
                table: "StockReceptionItems",
                column: "StockReceptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockReceptionItems");

            migrationBuilder.DropTable(
                name: "SupplierProductMaps");

            migrationBuilder.DropTable(
                name: "StockReceptions");
        }
    }
}
