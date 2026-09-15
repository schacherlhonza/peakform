using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

public class RecoveryMetric : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public int? RestingHeartRateBpm { get; set; }
    public int? ReadinessScore { get; set; }
    public int? StressScore { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
}
