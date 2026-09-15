using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// The resolved, canonical record of what an athlete actually did. Paired to a planned
/// workout by date+athlete (optionally an explicit link). Holds the fixed/common columns used
/// everywhere in the UI; less common numbers live on <see cref="ActivityMetric"/> instead of
/// growing this table's schema. See <see cref="DataProvenance"/> for where this record's data
/// came from and the dedup key.
/// </summary>
public class CompletedActivity : AuditableEntity, ISoftDeletable
{
    public Guid AthleteUserId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }

    public SportType Sport { get; set; } = SportType.Running;
    public string? Title { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public decimal? DistanceMeters { get; set; }
    public decimal? ElevationGainMeters { get; set; }
    public int? AverageHeartRateBpm { get; set; }
    public int? MaxHeartRateBpm { get; set; }
    public int? AveragePaceSecondsPerKm { get; set; }
    public int? AveragePowerWatts { get; set; }
    public int? Calories { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public DataProvenance? Provenance { get; set; }
    public ICollection<ActivityMetric> AdditionalMetrics { get; set; } = new List<ActivityMetric>();
}
