using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Nutrition;

public record FoodEntryDto(
    Guid Id,
    Guid AthleteUserId,
    DateTime ConsumedAtUtc,
    MealType MealType,
    string Description,
    decimal? EstimatedCarbsGrams,
    decimal? EstimatedProteinGrams,
    string? PhotoUrl,
    string? RelativeToWorkoutNote);

public record CreateFoodEntryRequest(
    Guid AthleteUserId,
    DateTime ConsumedAtUtc,
    MealType MealType,
    string Description,
    decimal? EstimatedCarbsGrams,
    decimal? EstimatedProteinGrams,
    string? PhotoUrl,
    string? RelativeToWorkoutNote);
