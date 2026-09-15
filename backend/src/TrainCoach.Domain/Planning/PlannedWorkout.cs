using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Planning;

/// <summary>
/// A single day's planned session. <see cref="CoachDescription"/> always carries the coach's
/// free-form text; <see cref="Segments"/> optionally adds structure on top (warm-up/intervals/
/// pauses/cool-down) — a coach is never forced to structure a simple easy run.
/// </summary>
public class PlannedWorkout : AuditableEntity, ISoftDeletable
{
    public Guid TrainingWeekId { get; set; }
    public TrainingWeek TrainingWeek { get; set; } = null!;

    public DateOnly Date { get; set; }
    public SportType Sport { get; set; } = SportType.Running;
    public string Title { get; set; } = string.Empty;
    public string CoachDescription { get; set; } = string.Empty;
    public bool IsRestDay { get; set; }

    public decimal? PlannedDistanceMeters { get; set; }
    public int? PlannedDurationSeconds { get; set; }
    public decimal? PlannedElevationGainMeters { get; set; }

    public Guid? CopiedFromWorkoutId { get; set; }
    public Guid? CopiedFromTemplateId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public ICollection<WorkoutSegment> Segments { get; set; } = new List<WorkoutSegment>();
}
