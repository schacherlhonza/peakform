using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public interface ITrainingSettingsSyncJob
{
    /// <summary>Writes the athlete's heart rate zones in effect today and their threshold pace to every
    /// connected provider implementing <see cref="ITrainingSettingsSyncProvider"/>; the outcome is stored on the connection.</summary>
    Task RunAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Queued after the zones or the threshold pace are saved and after a provider is (re)connected. Best
/// effort: a failure never affects the values in PeakForm, it's recorded as <c>HeartRateZonesSyncError</c>
/// (the column predates the threshold pace; it covers the whole settings write) for the UI.
/// Zones dated in the future are written once a later save or reconnect runs this again.
/// </summary>
public class TrainingSettingsSyncJob(
    IApplicationDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    IAccessTokenResolver tokenResolver,
    IDateTimeProvider clock,
    ILogger<TrainingSettingsSyncJob> logger) : ITrainingSettingsSyncJob
{
    public async Task RunAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        var syncProviders = providers.OfType<ITrainingSettingsSyncProvider>().Cast<IIntegrationProvider>().ToDictionary(p => p.ProviderType);
        var connections = await db.IntegrationConnections
            .Include(c => c.Credential)
            .Where(c => c.AthleteUserId == athleteUserId && c.Status == IntegrationConnectionStatus.Connected)
            .ToListAsync(cancellationToken);
        connections = connections.Where(c => syncProviders.ContainsKey(c.Provider)).ToList();
        if (connections.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var effectiveFrom = await db.HeartRateZones
            .Where(z => z.AthleteUserId == athleteUserId && z.EffectiveFromDate <= today)
            .MaxAsync(z => (DateOnly?)z.EffectiveFromDate, cancellationToken);
        var zones = effectiveFrom is null ? [] : await db.HeartRateZones
            .Where(z => z.AthleteUserId == athleteUserId && z.EffectiveFromDate == effectiveFrom)
            .OrderBy(z => z.ZoneNumber)
            .Select(z => new HeartRateZoneBound(z.ZoneNumber, z.Name, z.MaxBpm))
            .ToListAsync(cancellationToken);
        var thresholdPace = await db.AthleteProfiles
            .Where(p => p.UserProfileId == athleteUserId)
            .Select(p => p.ThresholdPaceSecondsPerKm)
            .FirstOrDefaultAsync(cancellationToken);
        if (zones.Count == 0 && thresholdPace is null)
        {
            return; // nothing to write — the provider keeps its own settings
        }
        var update = new TrainingSettingsUpdate(zones, thresholdPace);

        foreach (var connection in connections)
        {
            var provider = (ITrainingSettingsSyncProvider)syncProviders[connection.Provider];
            connection.HeartRateZonesSyncError = await PushAsync(connection.AthleteUserId, connection.Provider, connection.Credential?.GrantedScope, provider, update, cancellationToken);
            if (connection.HeartRateZonesSyncError is null)
            {
                connection.HeartRateZonesSyncedAtUtc = clock.UtcNow;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Null on success, otherwise the user-facing reason.</summary>
    private async Task<string?> PushAsync(
        Guid athleteUserId, IntegrationProviderType providerType, string? grantedScope, ITrainingSettingsSyncProvider provider,
        TrainingSettingsUpdate update, CancellationToken cancellationToken)
    {
        var zones = update.HeartRateZones;
        if (zones.Zip(zones.Skip(1)).Any(pair => pair.Second.MaxBpm <= pair.First.MaxBpm))
        {
            return "Horní hranice zón musí stoupat (Z1 < Z2 < …) — upravte zóny.";
        }
        var scopes = (grantedScope ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (!scopes.Contains(provider.SettingsSyncScope, StringComparer.OrdinalIgnoreCase))
        {
            return "Chybí oprávnění k zápisu nastavení — athlete musí znovu připojit účet.";
        }

        var accessToken = await tokenResolver.ResolveFreshAccessTokenAsync(athleteUserId, providerType, cancellationToken);
        if (accessToken is null)
        {
            return "Připojení vypršelo — athlete musí znovu připojit účet.";
        }

        try
        {
            await provider.PushTrainingSettingsAsync(accessToken, update, cancellationToken);
            return null;
        }
        catch (TrainingSettingsSyncException ex)
        {
            return ex.Message;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Zápis tréninkových nastavení do {Provider} selhal pro athleta {AthleteUserId}.", providerType, athleteUserId);
            return "Poskytovatel není dostupný — zkusí se znovu při příštím uložení zón nebo prahového tempa.";
        }
    }
}
