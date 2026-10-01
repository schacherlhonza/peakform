using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public interface IIntegrationConnectionService
{
    Task<IReadOnlyList<IntegrationConnectionDto>> GetForAthleteAsync(Guid callerUserId, Guid athleteUserId, CancellationToken cancellationToken = default);

    /// <summary>Only for providers where <see cref="IIntegrationProvider.RequiresOAuthRedirect"/> is true (Strava).</summary>
    Task<AuthorizationUrlDto> GetAuthorizationUrlAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);

    Task<IntegrationConnectionDto> HandleOAuthCallbackAsync(Guid callerUserId, IntegrationProviderType provider, string state, string code, CancellationToken cancellationToken = default);

    /// <summary>Only for demo providers (Garmin/MySASY) — no real redirect, connects immediately.</summary>
    Task<IntegrationConnectionDto> ConnectMockProviderAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);

    Task DisconnectAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);

    /// <summary>What disconnecting would delete — only Strava deletes data (API terms); others return zeros.</summary>
    Task<StravaDisconnectImpactDto> GetDisconnectImpactAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);

    Task TriggerSyncAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);

    /// <summary>Queues a one-off sync of older history from <paramref name="fromDate"/> (activities,
    /// wellness, and then detail streams). intervals.icu only — Strava history comes from the
    /// data archive import, the Strava API's limits and terms rule out a bulk pull.</summary>
    Task TriggerHistoryBackfillAsync(Guid callerUserId, IntegrationProviderType provider, DateOnly fromDate, CancellationToken cancellationToken = default);

    /// <summary>Queues a sync for every active connection of the caller. <paramref name="automatic"/>
    /// (the post-login sync) skips connections synced within the last few minutes.</summary>
    Task<IReadOnlyList<ProviderSyncStatusDto>> TriggerSyncAllAsync(Guid callerUserId, bool automatic, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderSyncStatusDto>> GetSyncStatusAsync(Guid callerUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SynchronizationRunDto>> GetSyncHistoryAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);
}
