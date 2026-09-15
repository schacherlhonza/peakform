using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// A morning or evening check-in. One entity with a <see cref="Type"/> discriminator rather
/// than table-per-hierarchy: the two check-ins share most dimensions (energy/fatigue/legs/
/// stress/note) and only differ in a few type-specific fields, so a subclass hierarchy would
/// add ceremony without payoff. Unique per (athlete, date, type).
/// Filling the basic check-in is meant to take about 30 seconds — keep required fields minimal.
/// </summary>
public class DailyCheckIn : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public CheckInType Type { get; set; }

    // Shared dimensions
    public WellnessScale? Energy { get; set; }
    public WellnessScale? Fatigue { get; set; }
    public WellnessScale? LegsFeeling { get; set; }
    public WellnessScale? Stress { get; set; }
    public bool? HasPainOrIllness { get; set; }
    public string? Note { get; set; }

    // Morning-only
    public WellnessScale? SleepQuality { get; set; }
    public WellnessScale? MuscleSoreness { get; set; }
    public WellnessScale? Motivation { get; set; }

    // Evening-only
    public decimal? HydrationLiters { get; set; }
    public WellnessScale? MealQuality { get; set; }
    public bool? CompletedPlannedWorkout { get; set; }
    public int? Rpe { get; set; }
}
