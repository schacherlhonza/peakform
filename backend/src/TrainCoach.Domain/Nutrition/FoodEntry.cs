using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Nutrition;

/// <summary>
/// Deliberately not a full calorie-tracking model for the MVP — free-text description plus
/// optional macro estimates, per the product scope.
/// </summary>
public class FoodEntry : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateTime ConsumedAtUtc { get; set; }
    public MealType MealType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? EstimatedCarbsGrams { get; set; }
    public decimal? EstimatedProteinGrams { get; set; }
    public string? PhotoUrl { get; set; }
    public string? RelativeToWorkoutNote { get; set; }
}
