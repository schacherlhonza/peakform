using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Planning;

public record WorkoutSegmentDto(
    Guid? Id,
    int Order,
    WorkoutSegmentType Type,
    int? RepeatCount,
    decimal? DistanceMeters,
    int? DurationSeconds,
    IntensityTargetType IntensityTargetType,
    Guid? TargetHeartRateZoneId,
    int? TargetPaceSecondsPerKmMin,
    int? TargetPaceSecondsPerKmMax,
    int? TargetRpe,
    int? TargetPowerWatts,
    string? Notes);

public record PlannedWorkoutDto(
    Guid Id,
    Guid TrainingWeekId,
    DateOnly Date,
    SportType Sport,
    string Title,
    string CoachDescription,
    bool IsRestDay,
    decimal? PlannedDistanceMeters,
    int? PlannedDurationSeconds,
    decimal? PlannedElevationGainMeters,
    IReadOnlyList<WorkoutSegmentDto> Segments);

public record CreatePlannedWorkoutRequest(
    Guid TrainingWeekId,
    DateOnly Date,
    SportType Sport,
    string Title,
    string CoachDescription,
    bool IsRestDay,
    decimal? PlannedDistanceMeters,
    int? PlannedDurationSeconds,
    decimal? PlannedElevationGainMeters,
    IReadOnlyList<WorkoutSegmentDto>? Segments);

public record UpdatePlannedWorkoutRequest(
    DateOnly Date,
    SportType Sport,
    string Title,
    string CoachDescription,
    bool IsRestDay,
    decimal? PlannedDistanceMeters,
    int? PlannedDurationSeconds,
    decimal? PlannedElevationGainMeters,
    IReadOnlyList<WorkoutSegmentDto>? Segments);

public record CopyWorkoutRequest(Guid TargetTrainingWeekId, DateOnly TargetDate);

public record TrainingWeekDto(
    Guid Id,
    Guid TrainingPlanId,
    DateOnly WeekStartDate,
    int WeekIndex,
    string? CoachWeekSummary,
    string? AthleteWeekReflection,
    string? WhatWasMissingNote,
    int? AthleteWeeklyRating,
    IReadOnlyList<PlannedWorkoutDto> Workouts);

public record CreateTrainingWeekRequest(DateOnly WeekStartDate, int WeekIndex);

public record UpdateTrainingWeekRequest(
    string? CoachWeekSummary,
    string? AthleteWeekReflection,
    string? WhatWasMissingNote,
    int? AthleteWeeklyRating);

public record TrainingPlanDto(
    Guid Id,
    Guid AthleteUserId,
    Guid CoachUserId,
    Guid? SeasonId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive);

public record TrainingPlanDetailDto(
    Guid Id,
    Guid AthleteUserId,
    Guid CoachUserId,
    Guid? SeasonId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    IReadOnlyList<TrainingWeekDto> Weeks);

public record CreateTrainingPlanRequest(Guid AthleteUserId, string Name, DateOnly StartDate, DateOnly? EndDate, Guid? SeasonId);
