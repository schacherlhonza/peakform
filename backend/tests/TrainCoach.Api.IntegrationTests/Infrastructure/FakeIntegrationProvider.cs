using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A test-double provider standing in for a real adapter (Strava, intervals.icu, ...) so sync
/// pipeline tests can control exactly what "arrives from the source" without any network call.
/// Returns whatever activities/wellness samples are in the mutable lists at the time
/// FetchRecentActivitiesAsync/FetchWellnessAsync are called.
/// </summary>
public class FakeIntegrationProvider(IntegrationProviderType providerType, List<ExternalActivity> activities, List<ExternalWellnessSample>? wellness = null)
    : IIntegrationProvider, IWellnessDataProvider
{
    public IntegrationProviderType ProviderType => providerType;
    public bool RequiresOAuthRedirect => false;

    public string BuildAuthorizationUrl(string state) => "https://fake.test/authorize";

    public Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalTokenResult("fake-access-token", null, null, "fake-account", null));

    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalTokenResult("fake-access-token-refreshed", null, null, "fake-account", null));

    public Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalActivity>>(activities.ToList());

    public Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalWellnessSample>>((wellness ?? []).ToList());
}
