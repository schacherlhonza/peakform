using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Planning;

/// <param name="Steps">Only on a <see cref="WorkoutSegmentType.Repeat"/> block: the steps it repeats (one level).</param>
public record WorkoutSegmentDto(
    Guid? Id,
    int Order,
    WorkoutSegmentType Type,
    int? RepeatCount,
    decimal? DistanceMeters,
    int? DurationSeconds,
    IntensityTargetType IntensityTargetType,
    Guid? TargetHeartRateZoneId,
    int? TargetHeartRateZoneNumber,
    int? TargetPaceSecondsPerKmMin,
    int? TargetPaceSecondsPerKmMax,
    int? TargetRpe,
    int? TargetPowerWatts,
    string? Notes,
    IReadOnlyList<WorkoutSegmentDto>? Steps = null);

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

/// <summary>Where a planned workout stands on one external calendar (intervals.icu → Garmin).</summary>
/// <param name="Warnings">Codes of what won't reach the watch as planned, e.g. <c>RpeSentAsText</c>.</param>
public record WorkoutPushStatusDto(
    IntegrationProviderType Provider,
    WorkoutPushStatus Status,
    DateTime? PushedAtUtc,
    DateTime UpdatedAtUtc,
    string? Error,
    IReadOnlyList<string> Warnings);

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
