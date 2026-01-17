using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freelaverse.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailConfirmationToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailConfirmationTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Mantém usuários existentes ativos, marcando-os como confirmados.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"EmailConfirmed\" = TRUE WHERE \"EmailConfirmed\" = FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmationToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "Users");
        }
    }
}
