namespace TrainCoach.Application.Integrations;

/// <summary>One zone as written to a provider: its upper bound — the next zone starts above it.</summary>
public record HeartRateZoneBound(int ZoneNumber, string Name, int MaxBpm);

/// <summary>
/// What PeakForm writes to the provider's training settings. Null/empty parts are left untouched there.
/// </summary>
/// <param name="HeartRateZones">Ordered, strictly increasing upper bounds; empty = keep the provider's zones.</param>
/// <param name="ThresholdPaceSecondsPerKm">Running threshold pace; null = keep the provider's value.</param>
public record TrainingSettingsUpdate(IReadOnlyList<HeartRateZoneBound> HeartRateZones, int? ThresholdPaceSecondsPerKm);

/// <summary>
/// Optional extra a provider adapter implements alongside <see cref="IIntegrationProvider"/> when it can
/// store the athlete's training settings (intervals.icu sport settings). PeakForm is the source of truth:
/// workouts pushed with a zone or pace target ("Z2 HR", "4:30/km Pace") resolve on the provider's side
/// against these settings — and without a threshold pace intervals.icu won't export run structure to
/// Garmin at all. See docs/integrations/garmin-workout-model.md §5.
/// </summary>
public interface ITrainingSettingsSyncProvider
{
    /// <summary>The OAuth scope the stored <c>GrantedScope</c> must contain; without it the athlete has to reconnect.</summary>
    string SettingsSyncScope { get; }

    /// <summary>Overwrites the provider's settings with the non-empty parts of <paramref name="update"/>.
    /// Throws <see cref="TrainingSettingsSyncException"/> with a user-facing message on rejection.</summary>
    Task PushTrainingSettingsAsync(string accessToken, TrainingSettingsUpdate update, CancellationToken cancellationToken = default);
}

public class TrainingSettingsSyncException(string message) : Exception(message);
