using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Integrations.Mock;

/// <summary>
/// Demo/mock stand-in for MySASY. Research (docs/integrations-research.md) found a beta "myAPI"
/// with a public Swagger spec, but real access is gated behind contacting MySASY directly and
/// couldn't be verified further in this session — so this mock exists until that access is
/// obtained. MySASY is primarily an HRV/readiness platform rather than an activity tracker, so
/// unlike Garmin's mock it returns no activities and instead implements
/// <see cref="IWellnessDataProvider"/> to simulate daily HRV/resting-HR samples.
/// </summary>
public class MySasyDemoProvider : IIntegrationProvider, IWellnessDataProvider
{
    public IntegrationProviderType ProviderType => IntegrationProviderType.MySasyDemoProvider;
    public bool RequiresOAuthRedirect => false;

    public string BuildAuthorizationUrl(string state) =>
        throw new NotSupportedException("MySASY demo provider connects directly, without an OAuth redirect.");

    public Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalTokenResult(
            AccessToken: $"demo-mysasy-token-{Guid.NewGuid():N}",
            RefreshToken: null,
            ExpiresAtUtc: null,
            ExternalAccountId: $"demo-mysasy-{Guid.NewGuid():N}"[..20],
            GrantedScope: "demo"));

    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        ExchangeCodeAsync("mock", cancellationToken);

    public Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalActivity>>([]);

    public Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var random = new Random(accessToken.GetHashCode());
        var days = Math.Min(14, Math.Max(1, (DateTime.UtcNow - sinceUtc).Days));
        var baselineHrv = 55 + random.Next(-10, 10);
        var baselineRhr = 50 + random.Next(-5, 5);

        var samples = new List<ExternalWellnessSample>();
        for (var i = 0; i < days; i++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i));
            samples.Add(new ExternalWellnessSample(
                date,
                HrvRmssdMs: baselineHrv + random.Next(-8, 8),
                RestingHeartRateBpm: baselineRhr + random.Next(-3, 3),
                ReadinessScore: 60 + random.Next(0, 35)));
        }

        return Task.FromResult<IReadOnlyList<ExternalWellnessSample>>(samples);
    }
}
