using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record HrvMeasurementDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    decimal RmssdMs,
    DataSource Source,
    DateTime? MeasuredAtUtc,
    string? Notes);

public record UpsertHrvMeasurementRequest(
    Guid AthleteUserId,
    DateOnly Date,
    decimal RmssdMs,
    DataSource Source,
    DateTime? MeasuredAtUtc,
    string? Notes);
