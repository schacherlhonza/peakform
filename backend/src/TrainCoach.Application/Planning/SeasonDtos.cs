namespace TrainCoach.Application.Planning;

public record SeasonDto(
    Guid Id,
    Guid AthleteUserId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);

public record CreateSeasonRequest(
    Guid AthleteUserId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);

public record UpdateSeasonRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);
