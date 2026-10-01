using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityBestEfforts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BestEffortsPrecise",
                table: "ActivityStreams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BestEffortsVersion",
                table: "ActivityStreams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ActivityBestEfforts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    StartOffsetSeconds = table.Column<int>(type: "integer", nullable: false),
                    ActivityStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsPrecise = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityBestEfforts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityBestEfforts_CompletedActivities_CompletedActivityId",
                        column: x => x.CompletedActivityId,
                        principalTable: "CompletedActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityBestEfforts_AthleteUserId_Sport_Type",
                table: "ActivityBestEfforts",
                columns: new[] { "AthleteUserId", "Sport", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityBestEfforts_CompletedActivityId_Type",
                table: "ActivityBestEfforts",
                columns: new[] { "CompletedActivityId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityBestEfforts");

            migrationBuilder.DropColumn(
                name: "BestEffortsPrecise",
                table: "ActivityStreams");

            migrationBuilder.DropColumn(
                name: "BestEffortsVersion",
                table: "ActivityStreams");
        }
    }
}
