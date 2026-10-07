using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutSegmentRepeatBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentSegmentId",
                table: "WorkoutSegments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSegments_ParentSegmentId",
                table: "WorkoutSegments",
                column: "ParentSegmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutSegments_WorkoutSegments_ParentSegmentId",
                table: "WorkoutSegments",
                column: "ParentSegmentId",
                principalTable: "WorkoutSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // "Repeat" used to be just a label on a flat segment; it now means a block with steps.
            // No block existed before this migration, so any such segment becomes a plain main step.
            migrationBuilder.Sql("""UPDATE "WorkoutSegments" SET "Type" = 2 WHERE "Type" = 6;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutSegments_WorkoutSegments_ParentSegmentId",
                table: "WorkoutSegments");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutSegments_ParentSegmentId",
                table: "WorkoutSegments");

            migrationBuilder.DropColumn(
                name: "ParentSegmentId",
                table: "WorkoutSegments");
        }
    }
}
