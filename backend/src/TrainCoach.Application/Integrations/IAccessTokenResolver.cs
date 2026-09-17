using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

/// <summary>
/// Single source of truth for "get me a usable access token for this athlete's connection",
/// including the refresh-if-expired step. Used both by <see cref="SyncOrchestrator"/> (recurring
/// sync) and by on-demand reads like a single activity's detail streams, so the refresh/persist
/// logic can't drift between the two call sites.
/// </summary>
public interface IAccessTokenResolver
{
    /// <returns>Null when the athlete has no connected credential for that provider.</returns>
    Task<string?> ResolveFreshAccessTokenAsync(Guid athleteUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default);
}
