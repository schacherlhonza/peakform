using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Integrations.Strava;

/// <summary>
/// Real Strava API v3 OAuth2 adapter (authorization_code flow). Endpoints, scopes and token
/// lifetime verified against developers.strava.com — see docs/integrations-research.md for the
/// exact URLs and verification date. Requires a real Strava API application's client id/secret
/// (see .env.example) to actually connect an account; without those, BuildAuthorizationUrl still
/// returns a URL but Strava will reject it, which is the expected "not configured yet" state.
/// </summary>
public class StravaIntegrationProvider(IHttpClientFactory httpClientFactory, IOptions<StravaOptions> options)
    : IIntegrationProvider
{
    private const string AuthorizeUrl = "https://www.strava.com/oauth/authorize";
    private const string TokenUrl = "https://www.strava.com/oauth/token";
    private const string ActivitiesUrl = "https://www.strava.com/api/v3/athlete/activities";

    private readonly StravaOptions _options = options.Value;

    public IntegrationProviderType ProviderType => IntegrationProviderType.Strava;
    public bool RequiresOAuthRedirect => true;

    public string BuildAuthorizationUrl(string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _options.ClientId;
        query["redirect_uri"] = _options.RedirectUri;
        query["response_type"] = "code";
        query["approval_prompt"] = "auto";
        query["scope"] = "activity:read_all";
        query["state"] = state;
        return $"{AuthorizeUrl}?{query}";
    }

    public async Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        RequireConfigured();

        var client = httpClientFactory.CreateClient(nameof(StravaIntegrationProvider));
        var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        }), cancellationToken);

        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    public async Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        RequireConfigured();

        var client = httpClientFactory.CreateClient(nameof(StravaIntegrationProvider));
        var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }), cancellationToken);

        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(StravaIntegrationProvider));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var afterUnixSeconds = new DateTimeOffset(DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var response = await client.GetAsync($"{ActivitiesUrl}?after={afterUnixSeconds}&per_page=100", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException($"Strava API vrátilo chybu {(int)response.StatusCode} při načítání aktivit.");
        }

        var activities = await response.Content.ReadFromJsonAsync<List<StravaActivity>>(cancellationToken: cancellationToken) ?? [];

        return activities.Select(a => new ExternalActivity(
            ExternalId: a.Id.ToString(),
            Sport: MapSport(a.SportType),
            Title: a.Name,
            StartedAtUtc: DateTime.SpecifyKind(a.StartDateUtc, DateTimeKind.Utc),
            DurationSeconds: a.MovingTimeSeconds,
            DistanceMeters: a.DistanceMeters,
            ElevationGainMeters: a.TotalElevationGainMeters,
            AverageHeartRateBpm: a.AverageHeartRate.HasValue ? (int)Math.Round(a.AverageHeartRate.Value) : null,
            MaxHeartRateBpm: a.MaxHeartRate.HasValue ? (int)Math.Round(a.MaxHeartRate.Value) : null,
            AveragePaceSecondsPerKm: a.AverageSpeedMetersPerSecond is > 0 ? (int)Math.Round(1000m / a.AverageSpeedMetersPerSecond.Value) : null,
            AveragePowerWatts: a.AverageWatts.HasValue ? (int)Math.Round(a.AverageWatts.Value) : null,
            Calories: a.Calories.HasValue ? (int)Math.Round(a.Calories.Value) : null)).ToList();
    }

    private void RequireConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new BusinessRuleException(
                "Strava integrace není nakonfigurována — chybí Client Id/Secret. Viz docs/integrations-research.md pro návod na aktivaci.");
        }
    }

    private static async Task<ExternalTokenResult> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BusinessRuleException($"Strava OAuth selhalo ({(int)response.StatusCode}): {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<StravaTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new BusinessRuleException("Strava vrátila neočekávanou odpověď.");

        return new ExternalTokenResult(
            token.AccessToken,
            token.RefreshToken,
            DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAtUnixSeconds).UtcDateTime,
            token.Athlete?.Id.ToString(),
            "activity:read_all");
    }

    private static SportType MapSport(string? stravaSportType) => stravaSportType?.ToLowerInvariant() switch
    {
        "run" or "trailrun" or "virtualrun" => SportType.Running,
        "ride" or "mountainbikeride" or "gravelride" or "virtualride" or "ebikeride" => SportType.Cycling,
        "swim" => SportType.Swimming,
        "weighttraining" or "workout" or "crossfit" => SportType.Strength,
        _ => SportType.Other,
    };
}
