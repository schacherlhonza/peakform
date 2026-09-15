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

    Task TriggerSyncAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SynchronizationRunDto>> GetSyncHistoryAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);
}
