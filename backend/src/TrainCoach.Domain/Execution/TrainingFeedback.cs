using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>The athlete's subjective feedback on a specific day/workout (RPE, legs, pain, free text).</summary>
public class TrainingFeedback : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public Guid? CompletedActivityId { get; set; }

    public DateOnly Date { get; set; }
    public int? Rpe { get; set; }
    public WellnessScale? LegsFeeling { get; set; }
    public int? OverallRating { get; set; }
    public string? PainNote { get; set; }
    public string? FreeText { get; set; }
}
