using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qvizz.Data.Migrations
{
    /// <inheritdoc />
    public partial class GameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuizJson",
                table: "games",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "Arranged",
                table: "answers",
                type: "integer[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuizJson",
                table: "games");

            migrationBuilder.DropColumn(
                name: "Arranged",
                table: "answers");
        }
    }
}
