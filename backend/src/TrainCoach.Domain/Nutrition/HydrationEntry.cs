using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Nutrition;

public class HydrationEntry : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateTime ConsumedAtUtc { get; set; }
    public HydrationDrinkType DrinkType { get; set; } = HydrationDrinkType.Water;
    public decimal VolumeMilliliters { get; set; }
    public bool ContainsElectrolytes { get; set; }
    public decimal? CaffeineMilligrams { get; set; }
    public string? Note { get; set; }
}
