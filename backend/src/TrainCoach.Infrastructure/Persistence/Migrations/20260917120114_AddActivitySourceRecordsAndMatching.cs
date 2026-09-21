using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitySourceRecordsAndMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DataProvenance -> ActivitySourceRecord: a rename+widen of an existing table (1:1
            // with CompletedActivity becomes many:1), NOT a drop+recreate — existing rows must
            // survive. See docs/integrations/canonical-data-and-deduplication-plan.md.
            migrationBuilder.RenameTable(name: "DataProvenances", newName: "ActivitySourceRecords");
            migrationBuilder.Sql("ALTER TABLE \"ActivitySourceRecords\" RENAME CONSTRAINT \"PK_DataProvenances\" TO \"PK_ActivitySourceRecords\";");
            migrationBuilder.Sql("ALTER TABLE \"ActivitySourceRecords\" RENAME CONSTRAINT \"FK_DataProvenances_CompletedActivities_CompletedActivityId\" TO \"FK_ActivitySourceRecords_CompletedActivities_CompletedActivity~\";");

            migrationBuilder.RenameIndex(
                table: "ActivitySourceRecords",
                name: "IX_DataProvenances_Source_ExternalId",
                newName: "IX_ActivitySourceRecords_Source_ExternalId");

            // Was unique (enforcing 1:1 with CompletedActivity); now several ActivitySourceRecords
            // can point at the same canonical activity, so the index becomes non-unique.
            migrationBuilder.DropIndex(name: "IX_DataProvenances_CompletedActivityId", table: "ActivitySourceRecords");
            migrationBuilder.CreateIndex(
                name: "IX_ActivitySourceRecords_CompletedActivityId",
                table: "ActivitySourceRecords",
                column: "CompletedActivityId");

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "ActivitySourceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FitFileUuid",
                table: "ActivitySourceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RawStartLatitude",
                table: "ActivitySourceRecords",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RawStartLongitude",
                table: "ActivitySourceRecords",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedFingerprint",
                table: "ActivitySourceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemsFlaggedForReview",
                table: "SynchronizationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Context",
                table: "SleepRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Context",
                table: "HrvMeasurements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchStatus",
                table: "CompletedActivities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedFingerprint",
                table: "CompletedActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimarySourceRecordId",
                table: "CompletedActivities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AthleteMetricSourcePrecedences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricKind = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteMetricSourcePrecedences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorDomainPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Domain = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    IsAthleteOverride = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorDomainPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyMetricSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MetricKind = table.Column<int>(type: "integer", nullable: false),
                    SelectedSource = table.Column<int>(type: "integer", nullable: false),
                    SelectedRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    PrecedenceRuleApplied = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMetricSelections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DuplicateCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityAId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityBId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    ScoringBreakdownJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MergeDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SurvivingActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsorbedSourceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsorbedActivityIdBeforeMerge = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ScoringBreakdownJson = table.Column<string>(type: "text", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevertedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevertedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompletedActivities_NormalizedFingerprint",
                table: "CompletedActivities",
                column: "NormalizedFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedActivities_PrimarySourceRecordId",
                table: "CompletedActivities",
                column: "PrimarySourceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySourceRecords_NormalizedFingerprint",
                table: "ActivitySourceRecords",
                column: "NormalizedFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteMetricSourcePrecedences_AthleteUserId_MetricKind_Sou~",
                table: "AthleteMetricSourcePrecedences",
                columns: new[] { "AthleteUserId", "MetricKind", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorDomainPolicies_AthleteUserId_Provider_Domain",
                table: "ConnectorDomainPolicies",
                columns: new[] { "AthleteUserId", "Provider", "Domain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMetricSelections_AthleteUserId_Date_MetricKind",
                table: "DailyMetricSelections",
                columns: new[] { "AthleteUserId", "Date", "MetricKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateCandidates_AthleteUserId_Status",
                table: "DuplicateCandidates",
                columns: new[] { "AthleteUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MergeDecisions_AthleteUserId",
                table: "MergeDecisions",
                column: "AthleteUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeDecisions_Outcome",
                table: "MergeDecisions",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_MergeDecisions_SurvivingActivityId",
                table: "MergeDecisions",
                column: "SurvivingActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AthleteMetricSourcePrecedences");
            migrationBuilder.DropTable(name: "ConnectorDomainPolicies");
            migrationBuilder.DropTable(name: "DailyMetricSelections");
            migrationBuilder.DropTable(name: "DuplicateCandidates");
            migrationBuilder.DropTable(name: "MergeDecisions");

            migrationBuilder.DropIndex(name: "IX_CompletedActivities_NormalizedFingerprint", table: "CompletedActivities");
            migrationBuilder.DropIndex(name: "IX_CompletedActivities_PrimarySourceRecordId", table: "CompletedActivities");
            migrationBuilder.DropIndex(name: "IX_ActivitySourceRecords_NormalizedFingerprint", table: "ActivitySourceRecords");

            migrationBuilder.DropColumn(name: "ItemsFlaggedForReview", table: "SynchronizationRuns");
            migrationBuilder.DropColumn(name: "Context", table: "SleepRecords");
            migrationBuilder.DropColumn(name: "Context", table: "HrvMeasurements");
            migrationBuilder.DropColumn(name: "MatchStatus", table: "CompletedActivities");
            migrationBuilder.DropColumn(name: "NormalizedFingerprint", table: "CompletedActivities");
            migrationBuilder.DropColumn(name: "PrimarySourceRecordId", table: "CompletedActivities");

            migrationBuilder.DropColumn(name: "DeviceName", table: "ActivitySourceRecords");
            migrationBuilder.DropColumn(name: "FitFileUuid", table: "ActivitySourceRecords");
            migrationBuilder.DropColumn(name: "RawStartLatitude", table: "ActivitySourceRecords");
            migrationBuilder.DropColumn(name: "RawStartLongitude", table: "ActivitySourceRecords");
            migrationBuilder.DropColumn(name: "NormalizedFingerprint", table: "ActivitySourceRecords");

            // Reverse of Up's rename+widen: restore the pre-rework shape (1:1, unique FK) without
            // losing rows, mirroring exactly how Up avoided a drop+recreate.
            migrationBuilder.DropIndex(name: "IX_ActivitySourceRecords_CompletedActivityId", table: "ActivitySourceRecords");
            migrationBuilder.CreateIndex(
                name: "IX_DataProvenances_CompletedActivityId",
                table: "ActivitySourceRecords",
                column: "CompletedActivityId",
                unique: true);

            migrationBuilder.RenameIndex(
                table: "ActivitySourceRecords",
                name: "IX_ActivitySourceRecords_Source_ExternalId",
                newName: "IX_DataProvenances_Source_ExternalId");

            migrationBuilder.Sql("ALTER TABLE \"ActivitySourceRecords\" RENAME CONSTRAINT \"FK_ActivitySourceRecords_CompletedActivities_CompletedActivity~\" TO \"FK_DataProvenances_CompletedActivities_CompletedActivityId\";");
            migrationBuilder.Sql("ALTER TABLE \"ActivitySourceRecords\" RENAME CONSTRAINT \"PK_ActivitySourceRecords\" TO \"PK_DataProvenances\";");
            migrationBuilder.RenameTable(name: "ActivitySourceRecords", newName: "DataProvenances");
        }
    }
}
