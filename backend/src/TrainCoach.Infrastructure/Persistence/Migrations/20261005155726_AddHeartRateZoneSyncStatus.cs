using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeartRateZoneSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeartRateZonesSyncError",
                table: "IntegrationConnections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HeartRateZonesSyncedAtUtc",
                table: "IntegrationConnections",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeartRateZonesSyncError",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "HeartRateZonesSyncedAtUtc",
                table: "IntegrationConnections");
        }
    }
}
