using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Reporting;

/// <summary>
/// Everything the rule engine and narrative composer need for one report, gathered once by
/// <see cref="IReportMetricsCalculator"/> so the two downstream steps never touch the database
/// themselves — keeps "what we measured" auditable and separate from "what we concluded".
/// </summary>
public record ReportInputSnapshot(
    Guid AthleteUserId,
    DateOnly Date,
    ReportType Type,
    string AthleteFirstName,
    // Check-in for this date/type, if filled in.
    WellnessScale? Energy,
    WellnessScale? Fatigue,
    WellnessScale? LegsFeeling,
    WellnessScale? Stress,
    WellnessScale? SleepQuality,
    bool? HasPainOrIllness,
    bool? CompletedPlannedWorkout,
    int? Rpe,
    // Recovery/wellness trend vs. personal baseline.
    int? RestingHeartRateBpm,
    decimal? BaselineRestingHeartRate,
    decimal? BaselineRestingHeartRateStdDev,
    decimal? HrvRmssdMs,
    decimal? BaselineHrv,
    decimal? BaselineHrvStdDev,
    int? SleepDurationMinutes,
    // Health flags currently active.
    bool HasActiveModerateOrSevereHealthFlag,
    // Training context.
    bool IsRestDay,
    string? TodayWorkoutTitle,
    decimal SevenDayDistanceMeters,
    int? DaysUntilNextRace,
    string? NextRaceName);
