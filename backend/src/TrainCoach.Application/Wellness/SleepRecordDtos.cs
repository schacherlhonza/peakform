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
    string? Notes);

public record UpsertSleepRecordRequest(
    Guid AthleteUserId,
    DateOnly Date,
    int? DurationMinutes,
    int? DeepSleepMinutes,
    int? RemSleepMinutes,
    DataSource Source,
    string? Notes);
