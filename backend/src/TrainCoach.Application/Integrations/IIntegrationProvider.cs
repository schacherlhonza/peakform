using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public record ExternalTokenResult(
    string AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAtUtc,
    string? ExternalAccountId,
    string? GrantedScope);

public record ExternalActivity(
    string ExternalId,
    SportType Sport,
    string? Title,
    DateTime StartedAtUtc,
    int DurationSeconds,
    decimal? DistanceMeters,
    decimal? ElevationGainMeters,
    int? AverageHeartRateBpm,
    int? MaxHeartRateBpm,
    int? AveragePaceSecondsPerKm,
    int? AveragePowerWatts,
    int? Calories,
    IReadOnlyList<ExternalActivityMetric>? AdditionalMetrics = null,
    string? RawPayloadJson = null,
    /// <summary>The recording device this provider reports, e.g. "Garmin Forerunner 965" — a
    /// signal for IActivityMatchingService's device-match scoring. Only intervals.icu populates
    /// this today.</summary>
    string? DeviceName = null,
    string? FitFileUuid = null);

/// <summary>One extra numeric metric for an activity that doesn't have its own fixed column on
/// <c>CompletedActivity</c> — written to the generic <c>ActivityMetric</c> table by <c>SyncOrchestrator</c>.</summary>
public record ExternalActivityMetric(ActivityMetricType Type, decimal Value, string Unit);

/// <summary>
/// The port every provider adapter implements — Strava for real, Garmin/MySASY as demo
/// providers until a real API is available (see docs/integrations-research.md). Application
/// depends only on this abstraction; concrete adapters live in TrainCoach.Integrations.
/// </summary>
public interface IIntegrationProvider
{
    IntegrationProviderType ProviderType { get; }

    /// <summary>True for providers with a real OAuth redirect flow (Strava). False for demo
    /// providers, which connect immediately via <see cref="ExchangeCodeAsync"/> with a placeholder code.</summary>
    bool RequiresOAuthRedirect { get; }

    string BuildAuthorizationUrl(string state);

    Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
