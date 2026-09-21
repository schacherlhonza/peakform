using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateDryRunReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuplicateDryRunReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExactTierCount = table.Column<int>(type: "integer", nullable: false),
                    HighConfidenceTierCount = table.Column<int>(type: "integer", nullable: false),
                    UncertainTierCount = table.Column<int>(type: "integer", nullable: false),
                    TotalActivitiesScanned = table.Column<int>(type: "integer", nullable: false),
                    CandidatesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateDryRunReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateDryRunReports_GeneratedAtUtc",
                table: "DuplicateDryRunReports",
                column: "GeneratedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuplicateDryRunReports");
        }
    }
}
