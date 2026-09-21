using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record SleepRecordDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    int? DurationMinutes,
    int? DeepSleepMinutes,
    int? RemSleepMinutes,
    DataSource Source,
    string? Notes,
    int? SleepScore = null,
    int? AvgSleepingHeartRateBpm = null);

public record UpsertSleepRecordRequest(
    Guid AthleteUserId,
    DateOnly Date,
    int? DurationMinutes,
    int? DeepSleepMinutes,
    int? RemSleepMinutes,
    DataSource Source,
    string? Notes);
