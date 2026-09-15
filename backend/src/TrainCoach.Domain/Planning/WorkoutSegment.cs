using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Planning;

/// <summary>
/// One structured block of a workout (warm-up, interval, rest, cool-down, ...). Belongs to
/// exactly one of <see cref="PlannedWorkout"/> or <see cref="WorkoutTemplate"/> — never both —
/// which the Application layer's validator enforces.
/// </summary>
public class WorkoutSegment : Entity
{
    public Guid? PlannedWorkoutId { get; set; }
    public PlannedWorkout? PlannedWorkout { get; set; }

    public Guid? WorkoutTemplateId { get; set; }
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public int Order { get; set; }
    public WorkoutSegmentType Type { get; set; }
    public int? RepeatCount { get; set; }

    public decimal? DistanceMeters { get; set; }
    public int? DurationSeconds { get; set; }

    public IntensityTargetType IntensityTargetType { get; set; } = IntensityTargetType.Free;
    public Guid? TargetHeartRateZoneId { get; set; }
    public int? TargetPaceSecondsPerKmMin { get; set; }
    public int? TargetPaceSecondsPerKmMax { get; set; }
    public int? TargetRpe { get; set; }
    public int? TargetPowerWatts { get; set; }

    public string? Notes { get; set; }
}
