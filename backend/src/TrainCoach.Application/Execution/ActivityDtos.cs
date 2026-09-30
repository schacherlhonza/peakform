using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Execution;

public record CompletedActivityDto(
    Guid Id,
    Guid AthleteUserId,
    Guid? PlannedWorkoutId,
    SportType Sport,
    string? Title,
    DateTime StartedAtUtc,
    int DurationSeconds,
    decimal? DistanceMeters,
    decimal? ElevationGainMeters,
    int? AverageHeartRateBpm,
    int? MaxHeartRateBpm,
    int? AveragePaceSecondsPerKm,
    int? AveragePowerWatts,
    int? Calories,
    DataSource Source,
    IReadOnlyList<ActivityMetricDto>? AdditionalMetrics = null,
    /// <summary>Recording device as reported by any source, e.g. "Garmin Forerunner 965" — also
    /// drives the Garmin attribution the intervals.icu API terms require for Garmin data.</summary>
    string? DeviceName = null);

/// <summary>Filter + page for the activity history list. <see cref="Search"/> matches the title
/// (case-insensitive substring); an empty <see cref="Sports"/> means all sports.</summary>
public record ActivitySearchQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    IReadOnlyList<SportType>? Sports = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>Totals over the whole filtered set (all pages), not just the returned page.</summary>
public record ActivityListSummaryDto(int Count, long TotalDurationSeconds, decimal TotalDistanceMeters, decimal TotalElevationGainMeters);

public record ActivityListPageDto(
    IReadOnlyList<CompletedActivityDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ActivityListSummaryDto Summary);

/// <summary>A metric that doesn't have its own fixed column on <see cref="CompletedActivityDto"/> (e.g. training load, elapsed time) — see <see cref="TrainCoach.Domain.Execution.ActivityMetric"/>.</summary>
public record ActivityMetricDto(ActivityMetricType Type, decimal Value, string Unit);

public enum ActivityStreamSource
{
    /// <summary>Read from our own <c>ActivityStream</c> table (parsed from the Strava archive).</summary>
    Stored = 1,

    /// <summary>Fetched on demand from the provider's API — never persisted (Strava API terms,
    /// see security.md).</summary>
    Live = 2,
}

/// <summary>
/// One activity's detail streams (heart rate, cadence, power, elevation, pace, grade, and — for
/// stored streams — GPS and temperature). Each array is either null (that stream wasn't recorded
/// for this activity — e.g. no power meter) or the same length as <see cref="TimeOffsetsSeconds"/>,
/// index-aligned (chart hover at index i = map position at index i). Running cadence is in steps
/// per minute (both feet), whatever the source's own convention.
/// </summary>
public record ActivityStreamsDto(
    Guid ActivityId,
    IReadOnlyList<int> TimeOffsetsSeconds,
    IReadOnlyList<int?>? HeartRateBpm,
    IReadOnlyList<int?>? CadenceRpm,
    IReadOnlyList<int?>? PowerWatts,
    IReadOnlyList<decimal?>? DistanceMeters,
    IReadOnlyList<decimal?>? ElevationMeters,
    IReadOnlyList<int?>? PaceSecondsPerKm,
    IReadOnlyList<decimal?>? GradePercent,
    IReadOnlyList<double?>? Latitude = null,
    IReadOnlyList<double?>? Longitude = null,
    IReadOnlyList<decimal?>? TemperatureC = null,
    ActivityStreamSource Source = ActivityStreamSource.Live,
    bool IsDownsampled = false);

public record CreateManualActivityRequest(
    Guid AthleteUserId,
    Guid? PlannedWorkoutId,
    SportType Sport,
    string? Title,
    DateTime StartedAtUtc,
    int DurationSeconds,
    decimal? DistanceMeters,
    decimal? ElevationGainMeters,
    int? AverageHeartRateBpm,
    int? MaxHeartRateBpm,
    int? AveragePaceSecondsPerKm,
    int? AveragePowerWatts,
    int? Calories);

public record TrainingFeedbackDto(
    Guid Id,
    Guid AthleteUserId,
    Guid? PlannedWorkoutId,
    Guid? CompletedActivityId,
    DateOnly Date,
    int? Rpe,
    WellnessScale? LegsFeeling,
    int? OverallRating,
    string? PainNote,
    string? FreeText);

public record UpsertTrainingFeedbackRequest(
    Guid AthleteUserId,
    Guid? PlannedWorkoutId,
    Guid? CompletedActivityId,
    DateOnly Date,
    int? Rpe,
    WellnessScale? LegsFeeling,
    int? OverallRating,
    string? PainNote,
    string? FreeText);
