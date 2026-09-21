using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record TrainingLoadSnapshotDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    decimal? Ctl,
    decimal? Atl,
    decimal? RampRate,
    DataSource Source);
