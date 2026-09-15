using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record PersonalRecordDto(
    Guid Id,
    Guid AthleteUserId,
    SportType Sport,
    string DistanceLabel,
    int? TimeSeconds,
    DateOnly AchievedDate,
    Guid? RaceId,
    string? Notes);

public record CreatePersonalRecordRequest(
    Guid AthleteUserId,
    SportType Sport,
    string DistanceLabel,
    int? TimeSeconds,
    DateOnly AchievedDate,
    Guid? RaceId,
    string? Notes);

public record UpdatePersonalRecordRequest(
    SportType Sport,
    string DistanceLabel,
    int? TimeSeconds,
    DateOnly AchievedDate,
    Guid? RaceId,
    string? Notes);
