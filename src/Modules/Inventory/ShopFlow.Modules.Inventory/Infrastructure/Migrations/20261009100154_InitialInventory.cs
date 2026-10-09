using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopFlow.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<string>(type: "text", nullable: false),
                    available_quantity = table.Column<int>(type: "integer", nullable: false),
                    reserved_quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.id);
                    table.CheckConstraint("CK_Inventory_Available", "available_quantity >= 0");
                    table.CheckConstraint("CK_Inventory_Reserved", "reserved_quantity >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_sku_id",
                schema: "inventory",
                table: "inventory_items",
                column: "sku_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "inventory");
        }
    }
}
