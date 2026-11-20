using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qvizz.Data.Migrations
{
    /// <inheritdoc />
    public partial class HostPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PausedRemainingMs",
                table: "games",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausedRemainingMs",
                table: "games");
        }
    }
}
