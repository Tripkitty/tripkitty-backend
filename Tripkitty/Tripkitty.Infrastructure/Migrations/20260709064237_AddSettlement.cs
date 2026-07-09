using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Trips",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsTransfer",
                table: "Expenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SettlementTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TripId = table.Column<string>(type: "text", nullable: false),
                    FromId = table.Column<string>(type: "text", nullable: false),
                    ToId = table.Column<string>(type: "text", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidMarkedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementTransactions_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementTransactions_TripId",
                table: "SettlementTransactions",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsTransfer",
                table: "Expenses");
        }
    }
}
