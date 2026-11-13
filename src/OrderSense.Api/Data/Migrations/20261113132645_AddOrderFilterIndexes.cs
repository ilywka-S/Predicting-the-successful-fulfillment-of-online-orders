using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderSense.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_orders_id_pattern",
                table: "orders",
                column: "id")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_customers_state",
                table: "customers",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_id_pattern",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_customers_state",
                table: "customers");
        }
    }
}
