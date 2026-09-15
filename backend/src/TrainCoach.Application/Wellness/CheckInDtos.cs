using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record DailyCheckInDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    CheckInType Type,
    WellnessScale? Energy,
    WellnessScale? Fatigue,
    WellnessScale? LegsFeeling,
    WellnessScale? Stress,
    bool? HasPainOrIllness,
    string? Note,
    WellnessScale? SleepQuality,
    WellnessScale? MuscleSoreness,
    WellnessScale? Motivation,
    decimal? HydrationLiters,
    WellnessScale? MealQuality,
    bool? CompletedPlannedWorkout,
    int? Rpe);

public record SubmitCheckInRequest(
    Guid AthleteUserId,
    DateOnly Date,
    CheckInType Type,
    WellnessScale? Energy,
    WellnessScale? Fatigue,
    WellnessScale? LegsFeeling,
    WellnessScale? Stress,
    bool? HasPainOrIllness,
    string? Note,
    WellnessScale? SleepQuality,
    WellnessScale? MuscleSoreness,
    WellnessScale? Motivation,
    decimal? HydrationLiters,
    WellnessScale? MealQuality,
    bool? CompletedPlannedWorkout,
    int? Rpe);
