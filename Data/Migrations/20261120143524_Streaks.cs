using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qvizz.Data.Migrations
{
    /// <inheritdoc />
    public partial class Streaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Streak",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StreakAfter",
                table: "answers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StreakBonus",
                table: "answers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Streak",
                table: "players");

            migrationBuilder.DropColumn(
                name: "StreakAfter",
                table: "answers");

            migrationBuilder.DropColumn(
                name: "StreakBonus",
                table: "answers");
        }
    }
}
