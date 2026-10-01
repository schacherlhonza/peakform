using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// The best effort of one kind inside one activity — fastest 5 km of a run, best 20-minute
/// average power of a ride — computed from its detail stream (BestEffortCalculator). Personal
/// records are derived from these at read time (best per type), never copied, so deleting or
/// merging an activity can't leave a stale record behind.
/// </summary>
public class ActivityBestEffort : Entity
{
    public Guid CompletedActivityId { get; set; }
    public CompletedActivity CompletedActivity { get; set; } = null!;

    public Guid AthleteUserId { get; set; }
    public SportType Sport { get; set; }
    public BestEffortType Type { get; set; }

    /// <summary>Seconds for a distance effort, watts for a power effort.</summary>
    public decimal Value { get; set; }

    /// <summary>Where in the activity the effort starts (seconds from its start).</summary>
    public int StartOffsetSeconds { get; set; }

    /// <summary>Copy of the activity's start — for record timelines without a join.</summary>
    public DateTime ActivityStartedAtUtc { get; set; }

    /// <summary>True when computed from the full-resolution file; false from the stored
    /// (downsampled) stream — a few seconds off at most.</summary>
    public bool IsPrecise { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
