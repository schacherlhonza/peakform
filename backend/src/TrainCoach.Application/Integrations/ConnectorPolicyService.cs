using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;

namespace TrainCoach.Application.Integrations;

public class ConnectorPolicyService(IApplicationDbContext db, IDateTimeProvider clock) : IConnectorPolicyService
{
    private static readonly DataDomain[] AllDomains = Enum.GetValues<DataDomain>();

    public async Task EnsureDefaultsAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        var connected = await ConnectedProvidersAsync(athleteUserId, cancellationToken);
        var existing = await db.ConnectorDomainPolicies
            .Where(p => p.AthleteUserId == athleteUserId)
            .ToListAsync(cancellationToken);

        foreach (var provider in connected)
        {
            foreach (var domain in AllDomains)
            {
                var defaultMode = DefaultMode(provider, domain, connected);
                var row = existing.FirstOrDefault(p => p.Provider == provider && p.Domain == domain);
                if (row is null)
                {
                    db.ConnectorDomainPolicies.Add(new ConnectorDomainPolicy
                    {
                        AthleteUserId = athleteUserId,
                        Provider = provider,
                        Domain = domain,
                        Mode = defaultMode,
                        IsAthleteOverride = false,
                        CreatedAtUtc = clock.UtcNow,
                    });
                }
                else if (!row.IsAthleteOverride && row.Mode != defaultMode)
                {
                    row.Mode = defaultMode;
                    row.UpdatedAtUtc = clock.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConnectorMode> GetEffectiveModeAsync(Guid athleteUserId, IntegrationProviderType provider, DataDomain domain, CancellationToken cancellationToken = default)
    {
        var row = await db.ConnectorDomainPolicies.FirstOrDefaultAsync(
            p => p.AthleteUserId == athleteUserId && p.Provider == provider && p.Domain == domain, cancellationToken);
        if (row is not null)
        {
            return row.Mode;
        }

        var connected = await ConnectedProvidersAsync(athleteUserId, cancellationToken);
        return DefaultMode(provider, domain, connected);
    }

    public async Task<IReadOnlyList<ConnectorDomainPolicyDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        var rows = await db.ConnectorDomainPolicies.Where(p => p.AthleteUserId == athleteUserId).ToListAsync(cancellationToken);
        return rows.Select(r => new ConnectorDomainPolicyDto(r.Provider, r.Domain, r.Mode, r.IsAthleteOverride)).ToList();
    }

    public async Task<ConnectorDomainPolicyDto> SetOverrideAsync(Guid athleteUserId, IntegrationProviderType provider, DataDomain domain, ConnectorMode mode, CancellationToken cancellationToken = default)
    {
        var row = await db.ConnectorDomainPolicies.FirstOrDefaultAsync(
            p => p.AthleteUserId == athleteUserId && p.Provider == provider && p.Domain == domain, cancellationToken);
        if (row is null)
        {
            row = new ConnectorDomainPolicy { AthleteUserId = athleteUserId, Provider = provider, Domain = domain, CreatedAtUtc = clock.UtcNow };
            db.ConnectorDomainPolicies.Add(row);
        }

        row.Mode = mode;
        row.IsAthleteOverride = true;
        row.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return new ConnectorDomainPolicyDto(provider, domain, mode, true);
    }

    private async Task<HashSet<IntegrationProviderType>> ConnectedProvidersAsync(Guid athleteUserId, CancellationToken cancellationToken)
    {
        var providers = await db.IntegrationConnections
            .Where(c => c.AthleteUserId == athleteUserId && c.Status == IntegrationConnectionStatus.Connected)
            .Select(c => c.Provider)
            .ToListAsync(cancellationToken);
        return providers.ToHashSet();
    }

    /// <summary>The default seed table — see docs/integrations/canonical-data-and-deduplication-plan.md.
    /// Non-overridden rows are recomputed from this whenever connection state changes, so e.g.
    /// connecting intervals.icu after Strava retroactively flips Strava's Activities default to
    /// FallbackOnly. Athlete overrides (IsAthleteOverride == true) are never recomputed.</summary>
    internal static ConnectorMode DefaultMode(IntegrationProviderType provider, DataDomain domain, IReadOnlySet<IntegrationProviderType> connectedProviders) => provider switch
    {
        IntegrationProviderType.Strava => domain switch
        {
            DataDomain.Activities => connectedProviders.Contains(IntegrationProviderType.IntervalsIcu) ? ConnectorMode.FallbackOnly : ConnectorMode.Primary,
            _ => ConnectorMode.Disabled,
        },
        IntegrationProviderType.IntervalsIcu => domain switch
        {
            DataDomain.BodyComposition => ConnectorMode.Secondary,
            _ => ConnectorMode.Primary,
        },
        IntegrationProviderType.GarminDemoProvider => domain switch
        {
            DataDomain.Activities => ConnectorMode.Secondary,
            _ => ConnectorMode.Disabled,
        },
        IntegrationProviderType.MySasyDemoProvider => domain switch
        {
            DataDomain.Hrv or DataDomain.RestingHeartRate or DataDomain.DailyWellness => ConnectorMode.Secondary,
            _ => ConnectorMode.Disabled,
        },
        IntegrationProviderType.Oura or IntegrationProviderType.Whoop => domain switch
        {
            DataDomain.Activities => ConnectorMode.EnrichmentOnly,
            DataDomain.Sleep or DataDomain.Hrv or DataDomain.RestingHeartRate => ConnectorMode.Secondary,
            DataDomain.VendorScores => ConnectorMode.Primary,
            _ => ConnectorMode.Disabled,
        },
        _ => ConnectorMode.Disabled,
    };
}
