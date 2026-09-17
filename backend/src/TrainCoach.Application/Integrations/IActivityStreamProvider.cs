namespace TrainCoach.Application.Integrations;

public record ExternalActivityStreams(
    IReadOnlyList<int> TimeOffsetsSeconds,
    IReadOnlyList<double?>? HeartRateBpm,
    IReadOnlyList<double?>? WattsOutput,
    IReadOnlyList<double?>? CadenceRpm,
    IReadOnlyList<double?>? DistanceMeters,
    IReadOnlyList<double?>? AltitudeMeters,
    IReadOnlyList<double?>? VelocityMetersPerSecond,
    IReadOnlyList<double?>? GradePercent);

/// <summary>
/// Optional extra a provider adapter can implement alongside <see cref="IIntegrationProvider"/>
/// when it can also supply one activity's second-by-second detail streams (heart rate, power,
/// cadence, elevation, pace) rather than just the activity summary — currently only Strava.
/// </summary>
public interface IActivityStreamProvider
{
    /// <returns>Null when the provider has no streams for this activity (e.g. sensor not used, or the activity no longer exists on the provider's side).</returns>
    Task<ExternalActivityStreams?> FetchActivityStreamsAsync(string accessToken, string externalActivityId, CancellationToken cancellationToken = default);
}
