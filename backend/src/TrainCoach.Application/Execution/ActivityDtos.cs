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
    DataSource Source);

/// <summary>
/// One activity's second-by-second detail streams (heart rate, cadence, power, elevation, pace,
/// grade), fetched live from the source provider — never persisted (see docs on the Strava data
/// display/retention obligations in security.md). Each array is either null (that stream wasn't
/// recorded for this activity — e.g. no power meter) or the same length as <see cref="TimeOffsetsSeconds"/>.
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
    IReadOnlyList<decimal?>? GradePercent);

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
