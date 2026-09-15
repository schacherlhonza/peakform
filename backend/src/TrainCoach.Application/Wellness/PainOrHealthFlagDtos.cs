using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record PainOrHealthFlagDto(
    Guid Id,
    Guid AthleteUserId,
    Guid? RelatedCheckInId,
    HealthFlagType Type,
    HealthFlagSeverity Severity,
    HealthFlagStatus Status,
    string? BodyPart,
    string? Description,
    DateOnly StartedOnDate,
    DateOnly? ResolvedOnDate);

public record CreatePainOrHealthFlagRequest(
    Guid AthleteUserId,
    Guid? RelatedCheckInId,
    HealthFlagType Type,
    HealthFlagSeverity Severity,
    string? BodyPart,
    string? Description,
    DateOnly StartedOnDate);

public record UpdateHealthFlagStatusRequest(HealthFlagStatus Status);
