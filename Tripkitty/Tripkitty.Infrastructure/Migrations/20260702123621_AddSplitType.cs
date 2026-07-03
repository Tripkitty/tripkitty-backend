using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSplitType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SplitType",
                table: "Expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Convert Share from ["id1","id2"] to [{"ParticipantId":"id1"},{"ParticipantId":"id2"}]
            migrationBuilder.Sql(@"
                UPDATE ""Expenses""
                SET ""Share"" = (
                    SELECT jsonb_agg(jsonb_build_object('ParticipantId', val))
                    FROM jsonb_array_elements_text(""Share"") AS val
                )
                WHERE jsonb_typeof(""Share"") = 'array'
                  AND jsonb_array_length(""Share"") > 0
                  AND jsonb_typeof(""Share""->0) = 'string';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SplitType",
                table: "Expenses");
        }
    }
}
