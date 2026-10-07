using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentHeartRateZoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetHeartRateZoneNumber",
                table: "WorkoutSegments",
                type: "integer",
                nullable: true);

            // Existing segments only reference a concrete zone row; carry its number over.
            migrationBuilder.Sql("""
                UPDATE "WorkoutSegments" s
                SET "TargetHeartRateZoneNumber" = z."ZoneNumber"
                FROM "HeartRateZones" z
                WHERE s."TargetHeartRateZoneId" = z."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetHeartRateZoneNumber",
                table: "WorkoutSegments");
        }
    }
}
