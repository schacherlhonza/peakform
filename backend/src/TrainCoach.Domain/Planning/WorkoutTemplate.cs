using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Planning;

/// <summary>
/// A reusable workout a coach can drop onto any day of any athlete's plan (feature: "vytvářet
/// šablony tréninků"). Not in the original entity list but required by the explicit template
/// feature request — kept minimal and shares the <see cref="WorkoutSegment"/> shape with
/// <see cref="PlannedWorkout"/> rather than duplicating it.
/// </summary>
public class WorkoutTemplate : AuditableEntity
{
    public Guid CoachUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SportType Sport { get; set; } = SportType.Running;
    public string Description { get; set; } = string.Empty;

    public ICollection<WorkoutSegment> Segments { get; set; } = new List<WorkoutSegment>();
}
