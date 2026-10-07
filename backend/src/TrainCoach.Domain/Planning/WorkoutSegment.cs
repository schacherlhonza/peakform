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

    /// <summary>
    /// Set on the steps of a repeat block (<see cref="WorkoutSegmentType.Repeat"/>). Steps still carry
    /// the owning workout/template id, so loading <c>Segments</c> returns the whole tree flat.
    /// One level only — intervals.icu (our Garmin path) has no nested repeats.
    /// </summary>
    public Guid? ParentSegmentId { get; set; }
    public WorkoutSegment? ParentSegment { get; set; }
    public ICollection<WorkoutSegment> Steps { get; set; } = new List<WorkoutSegment>();

    /// <summary>Position among its siblings (top level, or within the parent block).</summary>
    public int Order { get; set; }
    public WorkoutSegmentType Type { get; set; }

    /// <summary>On a repeat block: how many times its steps run. On a plain step: repeats of that one step.</summary>
    public int? RepeatCount { get; set; }

    /// <summary>
    /// The step's end condition — at most one of distance and duration (Garmin steps have exactly one);
    /// neither means "until lap press".
    /// </summary>
    public decimal? DistanceMeters { get; set; }
    public int? DurationSeconds { get; set; }

    public IntensityTargetType IntensityTargetType { get; set; } = IntensityTargetType.Free;
    public Guid? TargetHeartRateZoneId { get; set; }

    /// <summary>
    /// Zone 1–7 of the athlete's own zones. Unlike <see cref="TargetHeartRateZoneId"/> it isn't tied
    /// to one athlete's zone rows, so it works for templates and survives the athlete re-setting zones.
    /// </summary>
    public int? TargetHeartRateZoneNumber { get; set; }
    public int? TargetPaceSecondsPerKmMin { get; set; }
    public int? TargetPaceSecondsPerKmMax { get; set; }
    public int? TargetRpe { get; set; }
    public int? TargetPowerWatts { get; set; }

    public string? Notes { get; set; }
}
