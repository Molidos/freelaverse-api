using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freelaverse.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditsAndServiceValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Credits",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantProfessionals",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Value",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Credits",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QuantProfessionals",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Services");
        }
    }
}
