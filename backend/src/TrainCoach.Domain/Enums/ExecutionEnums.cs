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
}

public enum CommentAuthorRole
{
    Coach = 1,
    Athlete = 2,
}
