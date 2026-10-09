using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopFlow.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reference_id",
                schema: "ordering",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reference_id",
                schema: "ordering",
                table: "orders");
        }
    }
}
