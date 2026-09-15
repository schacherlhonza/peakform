using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Planning;

public class TrainingPlan : AuditableEntity, ISoftDeletable
{
    public Guid AthleteUserId { get; set; }
    public Guid CoachUserId { get; set; }
    public Guid? SeasonId { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public ICollection<TrainingWeek> Weeks { get; set; } = new List<TrainingWeek>();
}
