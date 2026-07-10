using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SponsorId",
                table: "TripMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SponsorId",
                table: "Guests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SponsorId",
                table: "TripMembers");

            migrationBuilder.DropColumn(
                name: "SponsorId",
                table: "Guests");
        }
    }
}
