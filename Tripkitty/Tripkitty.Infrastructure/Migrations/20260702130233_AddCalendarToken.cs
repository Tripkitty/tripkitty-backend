using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarToken",
                table: "TripMembers",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill unique tokens for existing rows
            migrationBuilder.Sql(
                "UPDATE \"TripMembers\" SET \"CalendarToken\" = replace(gen_random_uuid()::text, '-', '') WHERE \"CalendarToken\" = ''");

            migrationBuilder.CreateIndex(
                name: "IX_TripMembers_CalendarToken",
                table: "TripMembers",
                column: "CalendarToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripMembers_CalendarToken",
                table: "TripMembers");

            migrationBuilder.DropColumn(
                name: "CalendarToken",
                table: "TripMembers");
        }
    }
}
