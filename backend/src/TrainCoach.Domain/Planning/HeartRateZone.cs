using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Planning;

public class HeartRateZone : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public int ZoneNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinBpm { get; set; }
    public int MaxBpm { get; set; }
    public int? MinPaceSecondsPerKm { get; set; }
    public int? MaxPaceSecondsPerKm { get; set; }
    public DateOnly EffectiveFromDate { get; set; }
}
