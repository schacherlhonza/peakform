using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Planning;

public class Race : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public Guid? SeasonId { get; set; }
    public Guid? GoalId { get; set; }

    public string Name { get; set; } = string.Empty;
    public SportType Sport { get; set; } = SportType.Running;
    public DateTime StartsAtUtc { get; set; }
    public string? Location { get; set; }
    public decimal? DistanceMeters { get; set; }
    public decimal? ElevationGainMeters { get; set; }
    public GoalPriority Priority { get; set; } = GoalPriority.B;

    public int? TargetTimeSeconds { get; set; }
    public string? TargetResultNote { get; set; }

    public int? ActualTimeSeconds { get; set; }
    public string? ActualResultNote { get; set; }
    public string? ResultNotes { get; set; }
}
