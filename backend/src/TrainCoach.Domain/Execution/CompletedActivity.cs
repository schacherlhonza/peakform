using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// The resolved, canonical record of what an athlete actually did. Paired to a planned
/// workout by date+athlete (optionally an explicit link). Holds the fixed/common columns used
/// everywhere in the UI; less common numbers live on <see cref="ActivityMetric"/> instead of
/// growing this table's schema. Can be backed by more than one <see cref="ActivitySourceRecord"/>
/// (e.g. the same run reported by both Strava and intervals.icu) — see
/// <see cref="PrimarySourceRecordId"/> and IActivityMatchingService for how those get linked
/// instead of creating a second canonical activity.
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

    /// <summary>Id of whichever <see cref="ActivitySourceRecord"/> in <see cref="SourceRecords"/>
    /// currently populates this row's fixed columns — the source ranked highest by the connector
    /// policy in effect at merge time. Deliberately not modeled as an EF foreign key: a real FK in
    /// both directions (this activity's records point back at it via CompletedActivityId) would be
    /// a mutual/circular dependency EF's SaveChanges batching cannot order in a single call.
    /// Resolve it by matching against the already-loaded <see cref="SourceRecords"/> collection.
    /// Null only in the brief window between insert and backfill during the schema migration.</summary>
    public Guid? PrimarySourceRecordId { get; set; }

    public ActivityMatchStatus MatchStatus { get; set; } = ActivityMatchStatus.Unambiguous;

    /// <summary>Cache of the primary source record's fingerprint, kept on the canonical activity
    /// itself so the matcher can look up candidates without joining through every source record.
    /// A candidate index only — never itself the match decision. See IActivityMatchingService.</summary>
    public string? NormalizedFingerprint { get; set; }

    public ICollection<ActivitySourceRecord> SourceRecords { get; set; } = new List<ActivitySourceRecord>();
    public ICollection<ActivityMetric> AdditionalMetrics { get; set; } = new List<ActivityMetric>();
}
