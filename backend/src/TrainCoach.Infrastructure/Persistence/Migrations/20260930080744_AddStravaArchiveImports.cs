using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStravaArchiveImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StravaArchiveImportId",
                table: "ActivitySourceRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StravaArchiveImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    DownloadedBytes = table.Column<long>(type: "bigint", nullable: false),
                    StravaAthleteId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SportsFilter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreviewTotal = table.Column<int>(type: "integer", nullable: true),
                    PreviewInFilter = table.Column<int>(type: "integer", nullable: true),
                    PreviewAlreadyImported = table.Column<int>(type: "integer", nullable: true),
                    PreviewWouldCreate = table.Column<int>(type: "integer", nullable: true),
                    PreviewWouldMerge = table.Column<int>(type: "integer", nullable: true),
                    PreviewWouldReview = table.Column<int>(type: "integer", nullable: true),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: false),
                    ItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ItemsMerged = table.Column<int>(type: "integer", nullable: false),
                    ItemsFlaggedForReview = table.Column<int>(type: "integer", nullable: false),
                    ItemsSkippedDuplicate = table.Column<int>(type: "integer", nullable: false),
                    ItemsFailed = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviewReadyAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StravaArchiveImports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySourceRecords_StravaArchiveImportId",
                table: "ActivitySourceRecords",
                column: "StravaArchiveImportId");

            migrationBuilder.CreateIndex(
                name: "IX_StravaArchiveImports_AthleteUserId_CreatedAtUtc",
                table: "StravaArchiveImports",
                columns: new[] { "AthleteUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StravaArchiveImports_Status",
                table: "StravaArchiveImports",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StravaArchiveImports");

            migrationBuilder.DropIndex(
                name: "IX_ActivitySourceRecords_StravaArchiveImportId",
                table: "ActivitySourceRecords");

            migrationBuilder.DropColumn(
                name: "StravaArchiveImportId",
                table: "ActivitySourceRecords");
        }
    }
}
