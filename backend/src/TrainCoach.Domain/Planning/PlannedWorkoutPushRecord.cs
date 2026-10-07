using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Planning;

/// <summary>
/// The state of one planned workout on one external calendar (intervals.icu → Garmin). The mirror of
/// <c>ActivitySourceRecord</c> for the write direction: one row per workout and provider, so another
/// push target can be added without new columns on <see cref="PlannedWorkout"/>.
/// See docs/integrations/garmin-calendar-push.md §6.
/// </summary>
public class PlannedWorkoutPushRecord : Entity
{
    public Guid PlannedWorkoutId { get; set; }
    public PlannedWorkout PlannedWorkout { get; set; } = null!;

    public IntegrationProviderType Provider { get; set; }
    public WorkoutPushStatus Status { get; set; }

    /// <summary>The provider's own event id, from its response. We match by our workout id (external_id), this is for reference.</summary>
    public string? ExternalEventId { get; set; }

    public DateTime? PushedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>User-facing reason of the last failure; null otherwise.</summary>
    public string? Error { get; set; }

    /// <summary>Comma-separated warning codes (what won't reach the watch as planned), e.g. "RpeSentAsText".</summary>
    public string? Warnings { get; set; }
}
