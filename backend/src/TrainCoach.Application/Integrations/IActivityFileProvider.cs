namespace TrainCoach.Application.Integrations;

/// <summary>Thrown by a provider adapter on HTTP 429 — callers doing bulk work stop and resume later.</summary>
public class ProviderRateLimitedException(string message) : Exception(message);

/// <summary>
/// Optional extra a provider adapter implements when it can hand over an activity's original
/// device file (FIT/GPX/TCX, possibly gzipped) — currently intervals.icu. Unlike
/// <see cref="IActivityStreamProvider"/> (live, display-only), the parsed file may be stored.
/// </summary>
public interface IActivityFileProvider
{
    /// <returns>The raw file bytes, or null when the provider has no file for this activity.</returns>
    /// <exception cref="ProviderRateLimitedException">The provider's rate limit is exhausted.</exception>
    Task<byte[]?> DownloadActivityFileAsync(string accessToken, string externalActivityId, CancellationToken cancellationToken = default);
}
