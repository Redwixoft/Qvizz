using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qvizz.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialGameState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizTitle = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    PhaseEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PhaseLengthMs = table.Column<int>(type: "integer", nullable: false),
                    GroupRightOrder = table.Column<int[]>(type: "integer[]", nullable: false),
                    AnswerSeq = table.Column<long>(type: "bigint", nullable: false),
                    NextSeat = table.Column<int>(type: "integer", nullable: false),
                    RevealJson = table.Column<string>(type: "jsonb", nullable: true),
                    AwardsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Seat = table.Column<int>(type: "integer", nullable: false),
                    AvatarVersion = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    PrevRank = table.Column<int>(type: "integer", nullable: false),
                    LastGain = table.Column<int>(type: "integer", nullable: false),
                    Boost = table.Column<string>(type: "text", nullable: true),
                    BoostRivalId = table.Column<Guid>(type: "uuid", nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_players_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Choice = table.Column<int>(type: "integer", nullable: true),
                    Grouping = table.Column<int[]>(type: "integer[]", nullable: true),
                    Number = table.Column<double>(type: "double precision", nullable: true),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Changes = table.Column<int>(type: "integer", nullable: false),
                    ResponseMs = table.Column<int>(type: "integer", nullable: true),
                    RemainingMs = table.Column<int>(type: "integer", nullable: true),
                    Scored = table.Column<bool>(type: "boolean", nullable: false),
                    Correct = table.Column<bool>(type: "boolean", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    GoodPairs = table.Column<int>(type: "integer", nullable: false),
                    RankAfter = table.Column<int>(type: "integer", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answers", x => new { x.PlayerId, x.QuestionIndex });
                    table.ForeignKey(
                        name: "FK_answers_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "avatars",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Png = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avatars", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_avatars_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_answers_GameId",
                table: "answers",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_games_Status",
                table: "games",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_players_GameId",
                table: "players",
                column: "GameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "avatars");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
