using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesPOS.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelWithDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockReceptionItems_Barcode",
                table: "StockReceptionItems");

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "StockReceptionItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "StockReceptionItems",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceptionItems_Barcode",
                table: "StockReceptionItems",
                column: "Barcode",
                filter: "[Barcode] IS NOT NULL");
        }
    }
}
