using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public record ConnectorDomainPolicyDto(IntegrationProviderType Provider, DataDomain Domain, ConnectorMode Mode, bool IsAthleteOverride);

/// <summary>
/// Resolves and seeds <see cref="TrainCoach.Domain.Integrations.ConnectorDomainPolicy"/> rows.
/// Always athlete-scoped — see the entity's doc comment for why there is no global policy row.
/// </summary>
public interface IConnectorPolicyService
{
    /// <summary>Seeds/reseeds every non-overridden (<c>IsAthleteOverride == false</c>) policy row
    /// for this athlete from the default rule table. Called whenever a connection's state changes
    /// (connect/disconnect) so defaults like "Strava Activities=FallbackOnly once intervals.icu is
    /// connected" actually take effect automatically. Athlete overrides are never touched.</summary>
    Task EnsureDefaultsAsync(Guid athleteUserId, CancellationToken cancellationToken = default);

    Task<ConnectorMode> GetEffectiveModeAsync(Guid athleteUserId, IntegrationProviderType provider, DataDomain domain, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectorDomainPolicyDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);

    Task<ConnectorDomainPolicyDto> SetOverrideAsync(Guid athleteUserId, IntegrationProviderType provider, DataDomain domain, ConnectorMode mode, CancellationToken cancellationToken = default);
}
