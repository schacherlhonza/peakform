using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

public class PersonalRecord : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public SportType Sport { get; set; } = SportType.Running;
    public string DistanceLabel { get; set; } = string.Empty;
    public int? TimeSeconds { get; set; }
    public DateOnly AchievedDate { get; set; }
    public Guid? RaceId { get; set; }
    public string? Notes { get; set; }
}
