using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Loto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Draws",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DrawDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Number1 = table.Column<int>(type: "integer", nullable: false),
                    Number2 = table.Column<int>(type: "integer", nullable: false),
                    Number3 = table.Column<int>(type: "integer", nullable: false),
                    Number4 = table.Column<int>(type: "integer", nullable: false),
                    Number5 = table.Column<int>(type: "integer", nullable: false),
                    LuckyNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Draws", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Draws");
        }
    }
}
