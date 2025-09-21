using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesPOS.Migrations
{
    /// <inheritdoc />
    public partial class FixReceptionStatusDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Backfill παλιών εγγραφών: αν υπάρχει NULL στο Status, κάν’ το 0 (Draft)
            migrationBuilder.Sql(
                "UPDATE [dbo].[StockReceptions] SET [Status] = 0 WHERE [Status] IS NULL;"
            );

            // 2) Από εδώ και κάτω άσε ό,τι έχεις ήδη
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "StockReceptions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Mark",
                table: "StockReceptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "StockReceptionItems",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceptions_Mark",
                table: "StockReceptions",
                column: "Mark",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceptions_SupplierId",
                table: "StockReceptions",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceptionItems_Barcode",
                table: "StockReceptionItems",
                column: "Barcode",
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StockReceptions_Suppliers_SupplierId",
                table: "StockReceptions",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockReceptions_Suppliers_SupplierId",
                table: "StockReceptions");

            migrationBuilder.DropIndex(
                name: "IX_StockReceptions_Mark",
                table: "StockReceptions");

            migrationBuilder.DropIndex(
                name: "IX_StockReceptions_SupplierId",
                table: "StockReceptions");

            migrationBuilder.DropIndex(
                name: "IX_StockReceptionItems_Barcode",
                table: "StockReceptionItems");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "StockReceptions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Mark",
                table: "StockReceptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "StockReceptionItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
