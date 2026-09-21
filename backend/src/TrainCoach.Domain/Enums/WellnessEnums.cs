namespace TrainCoach.Domain.Enums;

public enum CheckInType
{
    Morning = 1,
    Evening = 2,
}

/// <summary>1 (worst) to 5 (best) subjective scale, used for sleep quality, energy, mood, etc.</summary>
public enum WellnessScale
{
    VeryPoor = 1,
    Poor = 2,
    Moderate = 3,
    Good = 4,
    VeryGood = 5,
}

public enum HealthFlagType
{
    Pain = 1,
    Illness = 2,
    Injury = 3,
    Other = 4,
}

public enum HealthFlagSeverity
{
    Mild = 1,
    Moderate = 2,
    Severe = 3,
}

public enum HealthFlagStatus
{
    Active = 1,
    Improving = 2,
    Resolved = 3,
}

/// <summary>Metric a rolling <see cref="TrainCoach.Domain.Wellness.PerformanceBaseline"/> is computed for.</summary>
public enum BaselineMetricType
{
    RestingHeartRate = 1,
    Hrv = 2,
    SleepDurationMinutes = 3,
    ReadinessScore = 4,
}

/// <summary>A wellness metric kind resolvable to a single "selected" value per athlete/day across
/// sources — see <see cref="TrainCoach.Domain.Wellness.DailyMetricSelection"/>. Distinct from
/// <see cref="BaselineMetricType"/>, which is about rolling baselines, not daily source selection.</summary>
public enum WellnessMetricKind
{
    Hrv = 1,
    RestingHeartRate = 2,
    SleepDurationMinutes = 3,
    SleepScore = 4,
    Weight = 5,
    Ctl = 6,
    Atl = 7,
    ReadinessScore = 8,
}

/// <summary>
/// What kind of reading a source's HRV/sleep row represents — descriptive metadata, not a dedup
/// dimension (the unique key stays AthleteUserId+Date+Source). Lets a future source distinguish,
/// e.g., a single daily spot-check HRV reading from a continuous overnight average. See Open
/// Question 3 in docs/integrations/canonical-data-and-deduplication-plan.md for the known limit:
/// today's schema can only hold one context per source per day.
/// </summary>
public enum MeasurementContext
{
    Unspecified = 0,
    MorningSpotHrv = 1,
    OvernightHrv = 2,
    OvernightSleep = 3,
    ManualEntry = 4,
}
