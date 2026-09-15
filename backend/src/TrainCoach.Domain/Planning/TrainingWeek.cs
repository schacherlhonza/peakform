using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Planning;

/// <summary>
/// A calendar week (Monday-start) within a <see cref="TrainingPlan"/>. Weekly totals
/// (distance, time, elevation, session count) are intentionally NOT stored here — they are
/// derived from <see cref="PlannedWorkout"/>/CompletedActivity data by the application layer,
/// to avoid two sources of truth for the same numbers.
/// </summary>
public class TrainingWeek : AuditableEntity
{
    public Guid TrainingPlanId { get; set; }
    public TrainingPlan TrainingPlan { get; set; } = null!;

    public DateOnly WeekStartDate { get; set; }
    public int WeekIndex { get; set; }

    public string? CoachWeekSummary { get; set; }
    public string? AthleteWeekReflection { get; set; }
    public string? WhatWasMissingNote { get; set; }
    public int? AthleteWeeklyRating { get; set; }

    public ICollection<PlannedWorkout> Workouts { get; set; } = new List<PlannedWorkout>();
}
