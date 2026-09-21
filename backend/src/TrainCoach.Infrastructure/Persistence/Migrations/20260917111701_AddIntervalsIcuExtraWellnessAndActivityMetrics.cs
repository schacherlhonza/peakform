using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntervalsIcuExtraWellnessAndActivityMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvgSleepingHeartRateBpm",
                table: "SleepRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SleepScore",
                table: "SleepRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FatigueScore",
                table: "RecoveryMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasInjurySignal",
                table: "RecoveryMetrics",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoodScore",
                table: "RecoveryMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotivationScore",
                table: "RecoveryMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SorenessScore",
                table: "RecoveryMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpO2Percent",
                table: "RecoveryMetrics",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Steps",
                table: "RecoveryMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Vo2Max",
                table: "RecoveryMetrics",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SdnnMs",
                table: "HrvMeasurements",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainingLoadSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Ctl = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Atl = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    RampRate = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingLoadSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeightMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightMeasurements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingLoadSnapshots_AthleteUserId_Date_Source",
                table: "TrainingLoadSnapshots",
                columns: new[] { "AthleteUserId", "Date", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeightMeasurements_AthleteUserId_Date_Source",
                table: "WeightMeasurements",
                columns: new[] { "AthleteUserId", "Date", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingLoadSnapshots");

            migrationBuilder.DropTable(
                name: "WeightMeasurements");

            migrationBuilder.DropColumn(
                name: "AvgSleepingHeartRateBpm",
                table: "SleepRecords");

            migrationBuilder.DropColumn(
                name: "SleepScore",
                table: "SleepRecords");

            migrationBuilder.DropColumn(
                name: "FatigueScore",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "HasInjurySignal",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "MoodScore",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "MotivationScore",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "SorenessScore",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "SpO2Percent",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "Steps",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "Vo2Max",
                table: "RecoveryMetrics");

            migrationBuilder.DropColumn(
                name: "SdnnMs",
                table: "HrvMeasurements");
        }
    }
}
