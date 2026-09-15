using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Planning;

public class Goal : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public Guid? SeasonId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? TargetDate { get; set; }
    public GoalPriority Priority { get; set; } = GoalPriority.B;

    public bool IsAchieved { get; set; }
    public string? AchievedNotes { get; set; }
}
