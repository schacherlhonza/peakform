using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamBackfillAndHistorySync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HistoryFromUtc",
                table: "SynchronizationRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StreamFetchAttemptedAtUtc",
                table: "ActivitySourceRecords",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistoryFromUtc",
                table: "SynchronizationRuns");

            migrationBuilder.DropColumn(
                name: "StreamFetchAttemptedAtUtc",
                table: "ActivitySourceRecords");
        }
    }
}
