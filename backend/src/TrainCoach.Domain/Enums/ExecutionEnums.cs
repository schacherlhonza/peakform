namespace TrainCoach.Domain.Enums;

/// <summary>
/// Where a piece of data originated. Used on <see cref="TrainCoach.Domain.Execution.DataProvenance"/>
/// to record provenance per-value, since the same metric can arrive from several sources.
/// </summary>
public enum DataSource
{
    Manual = 1,
    FileImport = 2,
    Strava = 3,
    GarminDemoProvider = 4,
    MySasyDemoProvider = 5,
    IntervalsIcu = 6,

    /// <summary>Not yet activated — see docs/integrations/oura-whoop-activation.md.</summary>
    Oura = 7,

    /// <summary>Not yet activated — see docs/integrations/oura-whoop-activation.md.</summary>
    Whoop = 8,
}

/// <summary>
/// Whether a <see cref="TrainCoach.Domain.Execution.CompletedActivity"/> is linked to exactly one
/// source unambiguously, or was auto/manually merged from multiple sources, or still has an open
/// candidate awaiting human review. See <see cref="TrainCoach.Domain.Execution.DuplicateCandidate"/>
/// and <see cref="TrainCoach.Domain.Execution.MergeDecision"/> for the review/audit trail.
/// </summary>
public enum ActivityMatchStatus
{
    Unambiguous = 1,
    AutoMerged = 2,
    PendingReview = 3,
    Confirmed = 4,
    Reverted = 5,
}

/// <summary>How a <see cref="TrainCoach.Domain.Execution.MergeDecision"/> was reached — which level
/// of the matching cascade (see docs/integrations/activity-matching.md) produced it, or whether a
/// human made the call.</summary>
public enum MergeDecisionKind
{
    AutoIdempotent = 1,
    AutoExternalIdentity = 2,
    AutoFitIdentity = 3,
    AutoFingerprintExact = 4,
    AutoHighConfidence = 5,
    ManualConfirmed = 6,
    ManualRejectedAsDuplicate = 7,
}

public enum MergeDecisionOutcome
{
    Merged = 1,
    KeptSeparate = 2,
    Reverted = 3,
}

/// <summary>
/// Precedence order when the same metric is reported by multiple sources for the same
/// athlete/day (higher wins). Kept as an enum so the ordering is explicit and reviewable,
/// see docs/decisions for the default precedence rule.
/// </summary>
public enum SourcePrecedence
{
    Manual = 10,
    FileImport = 20,
    GarminDemoProvider = 25,
    MySasyDemoProvider = 25,
    Strava = 30,

    /// <summary>
    /// Ranked just above Strava: intervals.icu is itself an aggregator over real device data
    /// (Garmin/Polar/Oura/WHOOP/...) and is the only source of sleep/HRV/readiness wellness data
    /// among the connected providers, so it should win on the rare day both report the same metric.
    /// </summary>
    IntervalsIcu = 32,
}

public enum ActivityMetricType
{
    DistanceMeters = 1,
    DurationSeconds = 2,
    ElevationGainMeters = 3,
    AverageHeartRate = 4,
    MaxHeartRate = 5,
    AveragePaceSecondsPerKm = 6,
    AveragePowerWatts = 7,
    Calories = 8,

    /// <summary>Total activity duration including stops — <see cref="TrainCoach.Domain.Execution.CompletedActivity.DurationSeconds"/> is moving time only.</summary>
    ElapsedTimeSeconds = 9,
    TrainingLoad = 10,
    WorkJoules = 11,
    WeightedAveragePowerWatts = 12,
    Intensity = 13,
}

public enum CommentAuthorRole
{
    Coach = 1,
    Athlete = 2,
}
