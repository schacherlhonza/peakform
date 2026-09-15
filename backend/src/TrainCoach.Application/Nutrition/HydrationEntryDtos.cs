using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Nutrition;

public record HydrationEntryDto(
    Guid Id,
    Guid AthleteUserId,
    DateTime ConsumedAtUtc,
    HydrationDrinkType DrinkType,
    decimal VolumeMilliliters,
    bool ContainsElectrolytes,
    decimal? CaffeineMilligrams,
    string? Note);

public record CreateHydrationEntryRequest(
    Guid AthleteUserId,
    DateTime ConsumedAtUtc,
    HydrationDrinkType DrinkType,
    decimal VolumeMilliliters,
    bool ContainsElectrolytes,
    decimal? CaffeineMilligrams,
    string? Note);
