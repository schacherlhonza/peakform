using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record WeightMeasurementDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    decimal WeightKg,
    DataSource Source);

public record UpsertWeightMeasurementRequest(
    Guid AthleteUserId,
    DateOnly Date,
    decimal WeightKg,
    DataSource Source);
