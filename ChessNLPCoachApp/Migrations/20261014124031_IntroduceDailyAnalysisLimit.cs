using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessNLPCoachApp.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceDailyAnalysisLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyAnalysisUsages",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UtcDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyAnalysisUsages", x => new { x.UserId, x.UtcDate });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyAnalysisUsages");
        }
    }
}
