namespace TrainCoach.Application.Integrations;

/// <summary>
/// Optional extra a provider adapter can implement alongside <see cref="IIntegrationProvider"/>
/// when the provider has its own API for revoking an access token server-side (e.g. intervals.icu's
/// <c>DELETE /api/v1/disconnect-app</c>) — called by <see cref="IIntegrationConnectionService.DisconnectAsync"/>
/// before the local credential is deleted, so the token doesn't stay valid on the provider's side
/// after the athlete disconnects. Providers without such an API (Strava, the demo providers) don't
/// implement this — disconnecting just deletes the local credential, same as before.
/// </summary>
public interface IRevocableIntegrationProvider
{
    Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default);
}
