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
