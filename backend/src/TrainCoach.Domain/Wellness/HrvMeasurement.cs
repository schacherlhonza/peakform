using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

public class HrvMeasurement : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal RmssdMs { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    public DateTime? MeasuredAtUtc { get; set; }
    public string? Notes { get; set; }
}
