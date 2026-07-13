using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseSponsors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "Sponsors",
                table: "Expenses",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            // Backfill: существующие расходы получают снапшот текущего живого спонсорства
            // поездки — расчёты старых поездок не меняются ни на копейку.
            migrationBuilder.Sql("""
                WITH pairs AS (
                    SELECT "TripId", "UserId" AS dep, "SponsorId" AS sponsor
                    FROM "TripMembers" WHERE "SponsorId" IS NOT NULL
                    UNION ALL
                    SELECT "TripId", "Id" AS dep, "SponsorId" AS sponsor
                    FROM "Guests" WHERE "SponsorId" IS NOT NULL
                ),
                maps AS (
                    SELECT "TripId", jsonb_object_agg(dep, sponsor) AS map
                    FROM pairs GROUP BY "TripId"
                )
                UPDATE "Expenses" e SET "Sponsors" = m.map
                FROM maps m WHERE e."TripId" = m."TripId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sponsors",
                table: "Expenses");
        }
    }
}
