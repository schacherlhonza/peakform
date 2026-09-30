using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityStreams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemsStreamsAdded",
                table: "StravaArchiveImports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreviewStreamsToAdd",
                table: "StravaArchiveImports",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityStreams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivitySourceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    OriginalSampleCount = table.Column<int>(type: "integer", nullable: false),
                    Channels = table.Column<int>(type: "integer", nullable: false),
                    StartLatitude = table.Column<double>(type: "double precision", nullable: true),
                    StartLongitude = table.Column<double>(type: "double precision", nullable: true),
                    MinLatitude = table.Column<double>(type: "double precision", nullable: true),
                    MinLongitude = table.Column<double>(type: "double precision", nullable: true),
                    MaxLatitude = table.Column<double>(type: "double precision", nullable: true),
                    MaxLongitude = table.Column<double>(type: "double precision", nullable: true),
                    FormatVersion = table.Column<byte>(type: "smallint", nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityStreams_ActivitySourceRecords_ActivitySourceRecordId",
                        column: x => x.ActivitySourceRecordId,
                        principalTable: "ActivitySourceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityStreams_ActivitySourceRecordId",
                table: "ActivityStreams",
                column: "ActivitySourceRecordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityStreams");

            migrationBuilder.DropColumn(
                name: "ItemsStreamsAdded",
                table: "StravaArchiveImports");

            migrationBuilder.DropColumn(
                name: "PreviewStreamsToAdd",
                table: "StravaArchiveImports");
        }
    }
}
