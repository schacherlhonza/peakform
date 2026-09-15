using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record RecoveryMetricDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    int? RestingHeartRateBpm,
    int? ReadinessScore,
    int? StressScore,
    DataSource Source);

public record UpsertRecoveryMetricRequest(
    Guid AthleteUserId,
    DateOnly Date,
    int? RestingHeartRateBpm,
    int? ReadinessScore,
    int? StressScore,
    DataSource Source);
