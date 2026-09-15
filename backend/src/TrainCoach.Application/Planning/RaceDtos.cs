using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Planning;

public record RaceDto(
    Guid Id,
    Guid AthleteUserId,
    Guid? SeasonId,
    Guid? GoalId,
    string Name,
    SportType Sport,
    DateTime StartsAtUtc,
    string? Location,
    decimal? DistanceMeters,
    decimal? ElevationGainMeters,
    GoalPriority Priority,
    int? TargetTimeSeconds,
    string? TargetResultNote,
    int? ActualTimeSeconds,
    string? ActualResultNote,
    string? ResultNotes);

public record CreateRaceRequest(
    Guid AthleteUserId,
    Guid? SeasonId,
    Guid? GoalId,
    string Name,
    SportType Sport,
    DateTime StartsAtUtc,
    string? Location,
    decimal? DistanceMeters,
    decimal? ElevationGainMeters,
    GoalPriority Priority,
    int? TargetTimeSeconds,
    string? TargetResultNote);

// Also used to record the actual result once the race has taken place.
public record UpdateRaceRequest(
    Guid? SeasonId,
    Guid? GoalId,
    string Name,
    SportType Sport,
    DateTime StartsAtUtc,
    string? Location,
    decimal? DistanceMeters,
    decimal? ElevationGainMeters,
    GoalPriority Priority,
    int? TargetTimeSeconds,
    string? TargetResultNote,
    int? ActualTimeSeconds,
    string? ActualResultNote,
    string? ResultNotes);
