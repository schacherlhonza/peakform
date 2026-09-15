using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Planning;

public class Season : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Notes { get; set; }
}
