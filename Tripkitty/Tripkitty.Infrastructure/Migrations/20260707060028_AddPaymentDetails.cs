using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Tripkitty.Domain.Entities;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<PaymentDetails>(
                name: "PaymentDetails",
                table: "TripMembers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<PaymentDetails>(
                name: "PaymentDetails",
                table: "Guests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Banks = table.Column<List<string>>(type: "jsonb", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentMethods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_UserId",
                table: "PaymentMethods",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "PaymentDetails",
                table: "TripMembers");

            migrationBuilder.DropColumn(
                name: "PaymentDetails",
                table: "Guests");
        }
    }
}
