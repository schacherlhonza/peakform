using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Integrations.Mock;

/// <summary>
/// Demo/mock stand-in for Garmin, used because the real Garmin Health API / Connect Developer
/// Program requires a business/enterprise approval process not obtainable for this project (see
/// docs/integrations-research.md). Connecting is immediate (no real OAuth redirect) and
/// generates plausible-looking running activities so the rest of the pipeline (sync, dedup,
/// dashboards) can be exercised end-to-end. Replace with a real adapter behind the same
/// IIntegrationProvider contract once/if approval is granted — see the activation notes in
/// docs/integrations-research.md.
/// </summary>
public class GarminDemoProvider : IIntegrationProvider
{
    public IntegrationProviderType ProviderType => IntegrationProviderType.GarminDemoProvider;
    public bool RequiresOAuthRedirect => false;

    public string BuildAuthorizationUrl(string state) =>
        throw new NotSupportedException("Garmin demo provider connects directly, without an OAuth redirect.");

    public Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalTokenResult(
            AccessToken: $"demo-garmin-token-{Guid.NewGuid():N}",
            RefreshToken: null,
            ExpiresAtUtc: null,
            ExternalAccountId: $"demo-garmin-{Guid.NewGuid():N}"[..20],
            GrantedScope: "demo"));

    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        ExchangeCodeAsync("mock", cancellationToken);

    public Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var random = new Random(accessToken.GetHashCode());
        var activities = new List<ExternalActivity>();
        var daysSpan = Math.Max(1, (DateTime.UtcNow - sinceUtc).Days);
        var sessionCount = Math.Min(5, Math.Max(1, daysSpan / 3));

        for (var i = 0; i < sessionCount; i++)
        {
            var day = DateTime.UtcNow.AddDays(-random.Next(0, daysSpan)).Date.AddHours(7 + random.Next(0, 12));
            var distanceKm = 5 + random.Next(0, 12);
            activities.Add(new ExternalActivity(
                ExternalId: $"garmin-demo-{day:yyyyMMddHHmm}",
                Sport: SportType.Running,
                Title: "Běh (Garmin demo)",
                StartedAtUtc: day,
                DurationSeconds: distanceKm * (300 + random.Next(0, 60)),
                DistanceMeters: distanceKm * 1000,
                ElevationGainMeters: random.Next(10, 150),
                AverageHeartRateBpm: 140 + random.Next(0, 25),
                MaxHeartRateBpm: 165 + random.Next(0, 20),
                AveragePaceSecondsPerKm: 300 + random.Next(0, 60),
                AveragePowerWatts: null,
                Calories: distanceKm * (60 + random.Next(0, 15))));
        }

        return Task.FromResult<IReadOnlyList<ExternalActivity>>(activities);
    }
}
