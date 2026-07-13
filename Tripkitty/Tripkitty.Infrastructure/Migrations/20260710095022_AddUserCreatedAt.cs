using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripkitty.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // Реальная дата регистрации существующих юзеров неизвестна — считаем их
            // зарегистрированными в момент применения миграции (defaultValueSql уже это сделал).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");
        }
    }
}
