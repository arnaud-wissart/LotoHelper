using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDrawDayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DrawDayName",
                table: "Draws",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DrawDayName",
                table: "Draws");
        }
    }
}
