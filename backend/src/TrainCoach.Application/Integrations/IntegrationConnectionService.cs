using Microsoft.EntityFrameworkCore;
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
    IDateTimeProvider clock) : IIntegrationConnectionService
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
            db.IntegrationCredentials.Remove(connection.Credential);
        }
        connection.Status = IntegrationConnectionStatus.NotConnected;
        connection.DisconnectedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
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
            connection.Credential = new IntegrationCredential();
        }
        connection.Credential.EncryptedAccessToken = tokenEncryptor.Protect(token.AccessToken);
        connection.Credential.EncryptedRefreshToken = token.RefreshToken is null ? null : tokenEncryptor.Protect(token.RefreshToken);
        connection.Credential.AccessTokenExpiresAtUtc = token.ExpiresAtUtc;
        connection.Credential.GrantedScope = token.GrantedScope;

        await db.SaveChangesAsync(cancellationToken);

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
