using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

public class PainOrHealthFlag : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public Guid? RelatedCheckInId { get; set; }

    public HealthFlagType Type { get; set; }
    public HealthFlagSeverity Severity { get; set; }
    public HealthFlagStatus Status { get; set; } = HealthFlagStatus.Active;
    public string? BodyPart { get; set; }
    public string? Description { get; set; }

    public DateOnly StartedOnDate { get; set; }
    public DateOnly? ResolvedOnDate { get; set; }
}
