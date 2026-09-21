using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record RecoveryMetricDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    int? RestingHeartRateBpm,
    int? ReadinessScore,
    int? StressScore,
    DataSource Source,
    int? MoodScore = null,
    int? SorenessScore = null,
    int? FatigueScore = null,
    int? MotivationScore = null,
    int? Steps = null,
    decimal? SpO2Percent = null,
    decimal? Vo2Max = null,
    bool? HasInjurySignal = null);

public record UpsertRecoveryMetricRequest(
    Guid AthleteUserId,
    DateOnly Date,
    int? RestingHeartRateBpm,
    int? ReadinessScore,
    int? StressScore,
    DataSource Source);
