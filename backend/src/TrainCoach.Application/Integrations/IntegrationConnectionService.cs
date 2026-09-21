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

        // Reseed non-overridden policy defaults now that this provider is no longer connected —
        // e.g. disconnecting intervals.icu should flip Strava's Activities default back to Primary.
        await policyService.EnsureDefaultsAsync(callerUserId, cancellationToken);
    }

    public async Task TriggerSyncAsync(Guid callerUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections
            .FirstOrDefaultAsync(c => c.AthleteUserId == callerUserId && c.Provider == provider, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", provider);

        if (connection.Status != IntegrationConnectionStatus.Connected)
        {
            throw new BusinessRuleException("Propojení není aktivní.");
        }

        await jobQueue.QueueSyncRunAsync(connection.Id, SyncTrigger.Manual, cancellationToken);
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

        return runs.Select(r => new SynchronizationRunDto(
            r.Id, provider, r.Status, r.StartedAtUtc, r.FinishedAtUtc, r.ItemsFetched, r.ItemsCreated, r.ItemsSkippedDuplicate, r.ErrorMessage)).ToList();
    }

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

        await jobQueue.QueueSyncRunAsync(connection.Id, SyncTrigger.OnConnect, cancellationToken);

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
        c.Id, c.AthleteUserId, c.Provider, c.Status, c.ExternalAccountId, c.ConnectedAtUtc, c.LastSyncedAtUtc);
}
