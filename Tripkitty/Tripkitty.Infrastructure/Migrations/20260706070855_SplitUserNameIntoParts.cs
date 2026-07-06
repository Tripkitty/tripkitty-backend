using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitUserNameIntoParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Users",
                newName: "LastName");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Users",
                type: "text",
                nullable: true);

            // Старое поле Name трактуем как ФИО: 1-е слово — фамилия, 2-е — имя,
            // остаток — отчество; одиночное слово считаем именем
            migrationBuilder.Sql("""
                UPDATE "Users" u SET
                    "LastName"   = CASE WHEN cardinality(p.parts) >= 2 THEN p.parts[1] ELSE '' END,
                    "FirstName"  = CASE WHEN cardinality(p.parts) >= 2 THEN p.parts[2] ELSE coalesce(p.parts[1], '') END,
                    "MiddleName" = CASE WHEN cardinality(p.parts) >= 3 THEN array_to_string(p.parts[3:], ' ') END
                FROM (SELECT "Id", regexp_split_to_array(btrim("LastName"), '\s+') AS parts FROM "Users") p
                WHERE p."Id" = u."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Users" SET "LastName" = btrim(concat_ws(' ', "LastName", "FirstName", "MiddleName"));
                """);

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Users",
                newName: "Name");
        }
    }
}
