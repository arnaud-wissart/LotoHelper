using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialDrawFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfficialDrawId",
                table: "Draws",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Draws",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Draws_DrawDate_Number1_Number2_Number3_Number4_Number5_Luck~",
                table: "Draws",
                columns: new[] { "DrawDate", "Number1", "Number2", "Number3", "Number4", "Number5", "LuckyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Draws_OfficialDrawId",
                table: "Draws",
                column: "OfficialDrawId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Draws_DrawDate_Number1_Number2_Number3_Number4_Number5_Luck~",
                table: "Draws");

            migrationBuilder.DropIndex(
                name: "IX_Draws_OfficialDrawId",
                table: "Draws");

            migrationBuilder.DropColumn(
                name: "OfficialDrawId",
                table: "Draws");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Draws");
        }
    }
}
