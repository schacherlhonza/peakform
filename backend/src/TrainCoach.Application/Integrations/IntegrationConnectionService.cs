using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;

namespace TrainCoach.Application.Integrations;

/// <summary>
/// Connecting/disconnecting third-party accounts is treated as a personal-account action, not
/// athlete-data sharing — only the athlete themselves manages their own integrations, never a
/// coach, so this uses a plain identity check rather than <see cref="IRelationshipAccessGuard"/>.
/// </summary>
public class IntegrationConnectionService(
    IApplicationDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    ITokenEncryptor tokenEncryptor,
    IBackgroundJobQueue jobQueue,
    IConnectorPolicyService policyService,
    IStravaApiDataPurgeService stravaPurgeService,
    IDateTimeProvider clock,
    ILogger<IntegrationConnectionService> logger) : IIntegrationConnectionService
{
    public async Task<IReadOnlyList<IntegrationConnectionDto>> GetForAthleteAsync(Guid callerUserId, Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        EnsureSelf(callerUserId, athleteUserId);

        var connections = await db.IntegrationConnections
            .Where(c => c.AthleteUserId == athleteUserId)
            .ToListAsync(cancellationToken);

        return connections.Select(ToDto).ToList();
    }

    public async Task<AuthorizationUrlDto> GetAuthorizationUrlAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var providerImpl = ResolveProvider(provider);
        if (!providerImpl.RequiresOAuthRedirect)
        {
            throw new BusinessRuleException("Tento poskytovatel se připojuje přímo, bez OAuth přesměrování.");
        }

        var state = EncodeState(callerUserId, provider);
        return new AuthorizationUrlDto(providerImpl.BuildAuthorizationUrl(state), state);
    }

    public async Task<IntegrationConnectionDto> HandleOAuthCallbackAsync(Guid callerUserId, IntegrationProviderType provider, string state, string code, CancellationToken cancellationToken = default)
    {
        var (stateUserId, stateProvider, issuedAtUtc) = DecodeState(state);
        if (stateUserId != callerUserId || stateProvider != provider)
        {
            throw new ForbiddenAccessException("Neplatný nebo neodpovídající OAuth stav.");
        }
        if (clock.UtcNow - issuedAtUtc > TimeSpan.FromMinutes(15))
        {
            throw new BusinessRuleException("Platnost autorizačního požadavku vypršela, zkuste to prosím znovu.");
        }

        var providerImpl = ResolveProvider(provider);
        var token = await providerImpl.ExchangeCodeAsync(code, cancellationToken);
        return await UpsertConnectionAsync(callerUserId, provider, token, cancellationToken);
    }

    public async Task<IntegrationConnectionDto> ConnectMockProviderAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var providerImpl = ResolveProvider(provider);
        if (providerImpl.RequiresOAuthRedirect)
        {
            throw new BusinessRuleException("Tento poskytovatel vyžaduje standardní OAuth přihlášení.");
        }

        var token = await providerImpl.ExchangeCodeAsync("mock", cancellationToken);
        return await UpsertConnectionAsync(callerUserId, provider, token, cancellationToken);
    }

    public async Task DisconnectAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections.Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.AthleteUserId == callerUserId && c.Provider == provider, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", provider);

        if (connection.Credential is not null)
        {
            if (providers.FirstOrDefault(p => p.ProviderType == provider) is IRevocableIntegrationProvider revocable)
            {
                // Best-effort: a failed remote revoke (token already invalid, network hiccup)
                // must never block the athlete from disconnecting locally.
                try
                {
                    var accessToken = tokenEncryptor.Unprotect(connection.Credential.EncryptedAccessToken);
                    await revocable.RevokeAsync(accessToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Nepodařilo se odvolat token u poskytovatele {Provider} při odpojování — pokračuji lokálním odpojením.", provider);
                }
            }

            db.IntegrationCredentials.Remove(connection.Credential);
        }
        connection.Status = IntegrationConnectionStatus.NotConnected;
        connection.DisconnectedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        // Strava API Agreement: data obtained through the API is deleted once access ends
        // (the athlete's own data-archive import stays) — see StravaApiDataPurgeService.
        if (provider == IntegrationProviderType.Strava)
        {
            var impact = await stravaPurgeService.PurgeAsync(callerUserId, cancellationToken);
            logger.LogInformation(
                "Odpojení Stravy: smazáno {Deleted} aktivit, odebrán zdroj u {Removed}, ponecháno z archivu {Kept}.",
                impact.ActivitiesDeleted, impact.SourcesRemoved, impact.KeptFromArchive);
        }

        // Reseed non-overridden policy defaults now that this provider is no longer connected —
        // e.g. disconnecting intervals.icu should flip Strava's Activities default back to Primary.
        await policyService.EnsureDefaultsAsync(callerUserId, cancellationToken);
    }

    public Task<StravaDisconnectImpactDto> GetDisconnectImpactAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default) =>
        provider == IntegrationProviderType.Strava
            ? stravaPurgeService.GetImpactAsync(callerUserId, cancellationToken)
            : Task.FromResult(new StravaDisconnectImpactDto(0, 0, 0));

    public async Task TriggerSyncAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections
            .FirstOrDefaultAsync(c => c.AthleteUserId == callerUserId && c.Provider == provider, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", provider);

        if (connection.Status != IntegrationConnectionStatus.Connected)
        {
            throw new BusinessRuleException("Propojení není aktivní.");
        }

        await EnqueueSyncAsync(connection, SyncTrigger.Manual, cancellationToken);
    }

    public async Task<IntegrationConnectionDto> SetPushPlannedWorkoutsAsync(Guid callerUserId, IntegrationProviderType provider, bool enabled, CancellationToken cancellationToken = default)
    {
        if (ResolveProvider(provider) is not IPlannedWorkoutPushProvider)
        {
            throw new BusinessRuleException("Tento poskytovatel neumí přijímat naplánované tréninky.");
        }
        var connection = await db.IntegrationConnections
            .FirstOrDefaultAsync(c => c.AthleteUserId == callerUserId && c.Provider == provider, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", provider);
        if (connection.Status != IntegrationConnectionStatus.Connected)
        {
            throw new BusinessRuleException("Propojení není aktivní.");
        }

        connection.PushPlannedWorkouts = enabled;
        await db.SaveChangesAsync(cancellationToken);
        await jobQueue.QueuePlannedWorkoutPushForAthleteAsync(callerUserId, cancellationToken);
        return ToDto(connection);
    }

    public async Task TriggerHistoryBackfillAsync(Guid callerUserId, IntegrationProviderType provider, DateOnly fromDate, CancellationToken cancellationToken = default)
    {
        if (provider != IntegrationProviderType.IntervalsIcu)
        {
            throw new BusinessRuleException(provider == IntegrationProviderType.Strava
                ? "Starší historii ze Stravy naimportujte z exportu dat (Import historie ze Stravy)."
                : "Stažení starší historie tento poskytovatel nepodporuje.");
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);
        if (fromDate >= today || fromDate < new DateOnly(2000, 1, 1))
        {
            throw new BusinessRuleException("Zvolte datum v minulosti (nejdříve rok 2000).");
        }

        var connection = await db.IntegrationConnections
            .FirstOrDefaultAsync(c => c.AthleteUserId == callerUserId && c.Provider == provider, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", provider);

        if (connection.Status != IntegrationConnectionStatus.Connected)
        {
            throw new BusinessRuleException("Propojení není aktivní.");
        }

        var queued = await EnqueueSyncAsync(connection, SyncTrigger.HistoryBackfill, cancellationToken,
            historyFromUtc: fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (!queued)
        {
            throw new BusinessRuleException("Synchronizace právě probíhá. Starší historii si vyžádejte, až doběhne.");
        }
    }

    public async Task<IReadOnlyList<ProviderSyncStatusDto>> TriggerSyncAllAsync(Guid callerUserId, bool automatic, CancellationToken cancellationToken = default)
    {
        var connections = await GetSyncableConnectionsAsync(callerUserId, cancellationToken);
        var trigger = automatic ? SyncTrigger.OnLogin : SyncTrigger.Manual;

        foreach (var connection in connections)
        {
            if (automatic && connection.LastSyncedAtUtc is { } lastSynced && clock.UtcNow - lastSynced < AutomaticSyncMinInterval)
            {
                continue;
            }
            await EnqueueSyncAsync(connection, trigger, cancellationToken);
        }

        return await GetSyncStatusAsync(callerUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderSyncStatusDto>> GetSyncStatusAsync(Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var connections = await GetSyncableConnectionsAsync(callerUserId, cancellationToken);

        var result = new List<ProviderSyncStatusDto>(connections.Count);
        foreach (var connection in connections)
        {
            // One query per connection — an athlete has at most a handful of providers.
            var latestRun = await db.SynchronizationRuns
                .Where(r => r.IntegrationConnectionId == connection.Id)
                .OrderByDescending(r => r.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new ProviderSyncStatusDto(
                connection.Provider, connection.Status, connection.LastSyncedAtUtc,
                latestRun is null ? null : ToRunDto(latestRun, connection.Provider)));
        }

        return result;
    }

    public async Task<IReadOnlyList<SynchronizationRunDto>> GetSyncHistoryAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections
            .FirstOrDefaultAsync(c => c.AthleteUserId == callerUserId && c.Provider == provider, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", provider);

        var runs = await db.SynchronizationRuns
            .Where(r => r.IntegrationConnectionId == connection.Id)
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return runs.Select(r => ToRunDto(r, provider)).ToList();
    }

    /// <summary>A queued-or-running run older than this is treated as lost — the in-process queue
    /// isn't durable, so a restart mid-sync would otherwise leave it "running" forever and block
    /// every later sync of that connection.</summary>
    private static readonly TimeSpan ActiveRunTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The post-login sync skips connections synced more recently than this, so opening
    /// several tabs or re-logging in doesn't re-hit provider rate limits.</summary>
    private static readonly TimeSpan AutomaticSyncMinInterval = TimeSpan.FromMinutes(15);

    /// <summary>Connected plus Error — a failed sync flips the connection to Error, and a later
    /// successful run flips it back, so "sync all" doubles as the retry.</summary>
    private Task<List<IntegrationConnection>> GetSyncableConnectionsAsync(Guid athleteUserId, CancellationToken cancellationToken) =>
        db.IntegrationConnections
            .Where(c => c.AthleteUserId == athleteUserId
                && (c.Status == IntegrationConnectionStatus.Connected || c.Status == IntegrationConnectionStatus.Error))
            .OrderBy(c => c.Provider)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Records a <see cref="SyncRunStatus.Pending"/> run before queueing, so the UI can show
    /// "queued" immediately — <see cref="SyncOrchestrator"/> picks that row up instead of creating
    /// its own. No-op (returns false) when a run for this connection is already queued or running.
    /// </summary>
    private async Task<bool> EnqueueSyncAsync(IntegrationConnection connection, SyncTrigger trigger, CancellationToken cancellationToken, DateTime? historyFromUtc = null)
    {
        var activeRuns = await db.SynchronizationRuns
            .Where(r => r.IntegrationConnectionId == connection.Id
                && (r.Status == SyncRunStatus.Pending || r.Status == SyncRunStatus.Running))
            .ToListAsync(cancellationToken);

        var staleBefore = clock.UtcNow - ActiveRunTimeout;
        foreach (var stale in activeRuns.Where(r => r.StartedAtUtc < staleBefore))
        {
            stale.Status = SyncRunStatus.Failed;
            stale.FinishedAtUtc = clock.UtcNow;
            stale.ErrorMessage = "Synchronizace byla přerušena a nedokončila se.";
        }

        if (activeRuns.Any(r => r.StartedAtUtc >= staleBefore))
        {
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        db.SynchronizationRuns.Add(new SynchronizationRun
        {
            IntegrationConnectionId = connection.Id,
            Trigger = trigger,
            HistoryFromUtc = historyFromUtc,
            Status = SyncRunStatus.Pending,
            StartedAtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        await jobQueue.QueueSyncRunAsync(connection.Id, trigger, cancellationToken);
        return true;
    }

    private static SynchronizationRunDto ToRunDto(SynchronizationRun r, IntegrationProviderType provider) => new(
        r.Id, provider, r.Status, r.StartedAtUtc, r.FinishedAtUtc, r.ItemsFetched, r.ItemsCreated, r.ItemsSkippedDuplicate,
        r.ErrorMessage, r.Trigger, r.ItemsUpdated, r.ItemsFlaggedForReview);

    private async Task<IntegrationConnectionDto> UpsertConnectionAsync(Guid athleteUserId, IntegrationProviderType provider, ExternalTokenResult token, CancellationToken cancellationToken)
    {
        var connection = await db.IntegrationConnections.Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.AthleteUserId == athleteUserId && c.Provider == provider, cancellationToken);

        if (connection is null)
        {
            connection = new IntegrationConnection
            {
                AthleteUserId = athleteUserId,
                Provider = provider,
                CreatedAtUtc = clock.UtcNow,
            };
            db.IntegrationConnections.Add(connection);
        }

        connection.Status = IntegrationConnectionStatus.Connected;
        connection.ExternalAccountId = token.ExternalAccountId;
        connection.ConnectedAtUtc = clock.UtcNow;
        connection.DisconnectedAtUtc = null;

        if (connection.Credential is null)
        {
            // Entity.Id is client-generated (Guid.NewGuid() in a property initializer, not
            // database-generated) — when `connection` is an EXISTING tracked entity (not freshly
            // Added), EF's graph-fixup for a new dependent reached only via a navigation property
            // sees a non-default key and infers "Modified" (assumes the row already exists) rather
            // than "Added", producing an UPDATE that matches 0 rows instead of an INSERT. Explicit
            // Add() sidesteps that heuristic. Reproduced live 2026-09-21: reconnecting intervals.icu
            // after a disconnect (credential row genuinely deleted, connection row reused) failed
            // with DbUpdateConcurrencyException without this — see docs/integrations-research.md §6.
            connection.Credential = new IntegrationCredential();
            db.IntegrationCredentials.Add(connection.Credential);
        }
        connection.Credential.EncryptedAccessToken = tokenEncryptor.Protect(token.AccessToken);
        connection.Credential.EncryptedRefreshToken = token.RefreshToken is null ? null : tokenEncryptor.Protect(token.RefreshToken);
        connection.Credential.AccessTokenExpiresAtUtc = token.ExpiresAtUtc;
        connection.Credential.GrantedScope = token.GrantedScope;

        await db.SaveChangesAsync(cancellationToken);

        // Retroactively reseed non-overridden policy defaults for every provider this athlete has
        // — e.g. connecting intervals.icu after Strava flips Strava's Activities default to
        // FallbackOnly automatically, without touching any athlete override.
        await policyService.EnsureDefaultsAsync(athleteUserId, cancellationToken);

        await EnqueueSyncAsync(connection, SyncTrigger.OnConnect, cancellationToken);
        // A (re)connect may have just granted the scopes zone writing and workout pushing need.
        await jobQueue.QueueTrainingSettingsSyncAsync(athleteUserId, cancellationToken);
        await jobQueue.QueuePlannedWorkoutPushForAthleteAsync(athleteUserId, cancellationToken);

        return ToDto(connection);
    }

    private IIntegrationProvider ResolveProvider(IntegrationProviderType provider) =>
        providers.FirstOrDefault(p => p.ProviderType == provider)
            ?? throw new BusinessRuleException($"Poskytovatel {provider} není zaregistrován.");

    private static void EnsureSelf(Guid callerUserId, Guid athleteUserId)
    {
        if (callerUserId != athleteUserId)
        {
            throw new ForbiddenAccessException("Integrace může spravovat pouze vlastník účtu.");
        }
    }

    private string EncodeState(Guid userId, IntegrationProviderType provider)
    {
        var raw = $"{userId:N}|{provider}|{clock.UtcNow:O}";
        return tokenEncryptor.Protect(raw);
    }

    private (Guid UserId, IntegrationProviderType Provider, DateTime IssuedAtUtc) DecodeState(string state)
    {
        string raw;
        try
        {
            raw = tokenEncryptor.Unprotect(state);
        }
        catch (Exception ex)
        {
            throw new ForbiddenAccessException($"Neplatný OAuth stav: {ex.Message}");
        }

        var parts = raw.Split('|');
        if (parts.Length != 3 || !Guid.TryParse(parts[0], out var userId) || !Enum.TryParse<IntegrationProviderType>(parts[1], out var provider) || !DateTime.TryParse(parts[2], out var issuedAtUtc))
        {
            throw new ForbiddenAccessException("Neplatný formát OAuth stavu.");
        }

        return (userId, provider, DateTime.SpecifyKind(issuedAtUtc, DateTimeKind.Utc));
    }

    private static IntegrationConnectionDto ToDto(IntegrationConnection c) => new(
        c.Id, c.AthleteUserId, c.Provider, c.Status, c.ExternalAccountId, c.ConnectedAtUtc, c.LastSyncedAtUtc,
        c.HeartRateZonesSyncedAtUtc, c.HeartRateZonesSyncError, c.PushPlannedWorkouts);
}
