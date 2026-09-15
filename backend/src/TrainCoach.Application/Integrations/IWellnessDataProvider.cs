namespace TrainCoach.Application.Integrations;

public record ExternalWellnessSample(DateOnly Date, decimal? HrvRmssdMs, int? RestingHeartRateBpm, int? ReadinessScore);

/// <summary>
/// Optional extra a provider adapter can implement alongside <see cref="IIntegrationProvider"/>
/// when it also supplies recovery/wellness data (HRV, resting HR) rather than just activities —
/// e.g. the MySASY demo provider. <see cref="SyncOrchestrator"/> checks for this via a type
/// check on the resolved provider, so Strava/Garmin (activities only) don't need a no-op impl.
/// </summary>
public interface IWellnessDataProvider
{
    Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
