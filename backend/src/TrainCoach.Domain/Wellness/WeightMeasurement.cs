using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// Daily body weight time series — distinct from <see cref="TrainCoach.Domain.Identity.AthleteProfile.CurrentWeightKg"/>,
/// which is a single current-value field with no history. A sync/manual entry here also updates
/// that profile field to the latest value, so existing consumers of "current weight" stay accurate.
/// </summary>
public class WeightMeasurement : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal WeightKg { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
}
