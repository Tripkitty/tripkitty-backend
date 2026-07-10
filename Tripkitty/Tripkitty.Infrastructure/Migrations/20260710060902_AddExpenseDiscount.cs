using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountAmountMinor",
                table: "Expenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Expenses",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GrossAmountMinor",
                table: "Expenses",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmountMinor",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "GrossAmountMinor",
                table: "Expenses");
        }
    }
}
