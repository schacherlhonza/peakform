using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public class AccessTokenResolver(
    IApplicationDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    ITokenEncryptor tokenEncryptor,
    IDateTimeProvider clock) : IAccessTokenResolver
{
    public async Task<string?> ResolveFreshAccessTokenAsync(Guid athleteUserId, IntegrationProviderType provider, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections
            .Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.AthleteUserId == athleteUserId && c.Provider == provider, cancellationToken);

        if (connection?.Credential is null)
        {
            return null;
        }

        string accessToken;
        try
        {
            accessToken = tokenEncryptor.Unprotect(connection.Credential.EncryptedAccessToken);

            if (connection.Credential.AccessTokenExpiresAtUtc is { } expiresAt && expiresAt <= clock.UtcNow && connection.Credential.EncryptedRefreshToken is not null)
            {
                var providerImpl = providers.First(p => p.ProviderType == provider);
                var refreshed = await providerImpl.RefreshTokenAsync(tokenEncryptor.Unprotect(connection.Credential.EncryptedRefreshToken), cancellationToken);

                connection.Credential.EncryptedAccessToken = tokenEncryptor.Protect(refreshed.AccessToken);
                if (refreshed.RefreshToken is not null)
                {
                    connection.Credential.EncryptedRefreshToken = tokenEncryptor.Protect(refreshed.RefreshToken);
                }
                connection.Credential.AccessTokenExpiresAtUtc = refreshed.ExpiresAtUtc;
                accessToken = refreshed.AccessToken;

                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (CryptographicException)
        {
            // The stored ciphertext can no longer be decrypted — e.g. the Data Protection key
            // ring was reset (see docs on the DataProtection:KeysPath volume mount) after this
            // credential was encrypted. It's unrecoverable; surface it as "needs reconnecting"
            // rather than letting the exception bubble up as a 500.
            connection.Status = IntegrationConnectionStatus.Error;
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        return accessToken;
    }
}
