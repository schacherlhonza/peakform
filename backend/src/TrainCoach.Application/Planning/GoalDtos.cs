using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Planning;

public record GoalDto(
    Guid Id,
    Guid AthleteUserId,
    Guid? SeasonId,
    string Title,
    string? Description,
    DateOnly? TargetDate,
    GoalPriority Priority,
    bool IsAchieved,
    string? AchievedNotes);

public record CreateGoalRequest(
    Guid AthleteUserId,
    Guid? SeasonId,
    string Title,
    string? Description,
    DateOnly? TargetDate,
    GoalPriority Priority);

public record UpdateGoalRequest(
    Guid? SeasonId,
    string Title,
    string? Description,
    DateOnly? TargetDate,
    GoalPriority Priority,
    bool IsAchieved,
    string? AchievedNotes);
