using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>Objective sleep data (from a wearable sync or import) — distinct from the subjective SleepQuality on a morning check-in.</summary>
public class SleepRecord : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public int? DurationMinutes { get; set; }
    public int? DeepSleepMinutes { get; set; }
    public int? RemSleepMinutes { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    public string? Notes { get; set; }
}
