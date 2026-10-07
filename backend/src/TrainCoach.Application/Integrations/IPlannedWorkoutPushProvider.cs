using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Integrations;

/// <param name="ExternalEventId">The provider's id of the calendar entry.</param>
/// <param name="Warnings">Codes of what won't reach the watch as planned (e.g. <c>RpeSentAsText</c>).</param>
public record PlannedWorkoutPushResult(string? ExternalEventId, IReadOnlyList<string> Warnings);

/// <summary>
/// Optional extra a provider adapter implements alongside <see cref="IIntegrationProvider"/> when it can
/// put planned workouts on the athlete's calendar — intervals.icu, which uploads them on to Garmin Connect.
/// Entries are keyed by the PeakForm workout id (the provider's external id), so pushing again updates
/// in place and removal needs no stored provider id. See docs/integrations/garmin-calendar-push.md.
/// </summary>
public interface IPlannedWorkoutPushProvider
{
    /// <summary>The OAuth scope the stored <c>GrantedScope</c> must contain; without it the athlete has to reconnect.</summary>
    string WorkoutPushScope { get; }

    /// <summary>Creates or updates the calendar entry. Null when the workout has nothing to put on a
    /// watch (rest day) — the caller removes it instead. Throws <see cref="WorkoutPushException"/>.</summary>
    Task<PlannedWorkoutPushResult?> UpsertWorkoutAsync(string accessToken, PlannedWorkout workout, CancellationToken cancellationToken = default);

    /// <summary>Removes the entry for this workout; succeeds when there is none. Throws <see cref="WorkoutPushException"/>.</summary>
    Task RemoveWorkoutAsync(string accessToken, Guid plannedWorkoutId, CancellationToken cancellationToken = default);
}

/// <summary>A push the provider rejected, with a user-facing message.</summary>
public class WorkoutPushException(string message) : Exception(message);
