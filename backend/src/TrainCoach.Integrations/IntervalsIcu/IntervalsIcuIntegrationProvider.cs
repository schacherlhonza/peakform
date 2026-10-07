using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Integrations.IntervalsIcu;

/// <summary>
/// OAuth2 adapter for intervals.icu (authorization_code flow). intervals.icu is itself an
/// aggregator — an athlete who connects Garmin/Polar/Suunto/Coros/Huawei/Amazfit/Oura/WHOOP/Strava
/// to their own intervals.icu account gets all of that combined data through this single adapter,
/// which is how TrainCoach reaches Garmin/wellness data without Garmin's business-only Connect
/// Developer Program approval. See docs/integrations-research.md §5 for the endpoints, scopes and
/// field names verified (and the ones that could NOT be verified — the official Swagger UI
/// returned HTTP 500 during that research) as part of that write-up.
///
/// Two behaviours are deliberately different from <see cref="Strava.StravaIntegrationProvider"/>:
/// - intervals.icu's OAuth flow never issues a refresh token (confirmed by intervals.icu staff on
///   the community forum), so <see cref="RefreshTokenAsync"/> always throws — see its remarks.
/// - The activity-streams response shape is community-sourced, not officially confirmed, so
///   <see cref="FetchActivityStreamsAsync"/> treats a parse failure as "no streams" rather than
///   letting it fail the whole sync run.
/// </summary>
public class IntervalsIcuIntegrationProvider(IHttpClientFactory httpClientFactory, IOptions<IntervalsIcuOptions> options, ILogger<IntervalsIcuIntegrationProvider> logger)
    : IIntegrationProvider, IActivityStreamProvider, IActivityFileProvider, IWellnessDataProvider, IRevocableIntegrationProvider, ITrainingSettingsSyncProvider, IPlannedWorkoutPushProvider
{
    private const string AuthorizeUrl = "https://intervals.icu/oauth/authorize";

    // Confirmed live 2026-09-17 against a real approved OAuth app, after two wrong guesses:
    // "/api/v1/oauth/token" 404s (Spring Boot app respondeded, no such route), and bare
    // "/oauth/token" 405s at the edge/nginx layer before even reaching the app (that path is the
    // GET-only authorize/consent UI route, not an API endpoint). The real token endpoint is under
    // "/api/oauth/token" — no "/v1/" segment, unlike every other data endpoint in this adapter.
    private const string TokenUrl = "https://intervals.icu/api/oauth/token";
    private const string ApiBaseUrl = "https://intervals.icu/api/v1";
    // CALENDAR:WRITE added 2026-09-21 for a one-off experiment verifying whether pushing a planned
    // workout to intervals.icu's own calendar (POST /athlete/{id}/events) propagates onward to a
    // connected Garmin device — see docs/integrations-research.md §6. Athletes who already
    // authorized the app with the old scope list must reconnect for this to take effect
    // (intervals.icu replaces the whole granted-scope set on each new authorization).
    // SETTINGS:WRITE added 2026-10-05: PeakForm writes the athlete's heart rate zones into the Run
    // sport settings so pushed "Z2 HR" steps resolve against PeakForm's zones — same reconnect caveat.
    private const string Scopes = "ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE,SETTINGS:WRITE";

    /// <summary>The sport settings PeakForm's (sport-independent) zones are written to.</summary>
    private const string ZoneSportType = "Run";
    private const string StreamTypes = "time,heartrate,watts,cadence,distance,altitude,velocity_smooth,grade_smooth";

    private readonly IntervalsIcuOptions _options = options.Value;

    public IntegrationProviderType ProviderType => IntegrationProviderType.IntervalsIcu;
    public bool RequiresOAuthRedirect => true;

    public string BuildAuthorizationUrl(string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _options.ClientId;
        query["redirect_uri"] = _options.RedirectUri;
        query["response_type"] = "code";
        query["scope"] = Scopes;
        query["state"] = state;
        return $"{AuthorizeUrl}?{query}";
    }

    public async Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        RequireConfigured();

        var client = httpClientFactory.CreateClient(nameof(IntervalsIcuIntegrationProvider));
        var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        }), cancellationToken);

        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    /// <summary>
    /// Always throws: intervals.icu's OAuth implementation does not issue refresh tokens (per
    /// intervals.icu staff on the community forum — "it doesn't use refresh tokens, only access
    /// tokens"), so <see cref="ExchangeCodeAsync"/> always stores a null refresh token, and
    /// <c>AccessTokenResolver</c> therefore never calls this method in practice — it only refreshes
    /// when a refresh token was actually stored. If an intervals.icu access token is later revoked
    /// or stops working, the only remedy is for the athlete to reconnect via the OAuth flow again;
    /// this is surfaced as the connection flipping to <c>IntegrationConnectionStatus.Error</c> on
    /// the next failed sync, same as any other provider error.
    /// </summary>
    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        throw new BusinessRuleException(
            "intervals.icu nepoužívá refresh tokeny — přístup je nutné obnovit opětovným připojením účtu (OAuth tok od začátku).");

    public async Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);

        var oldest = DateOnly.FromDateTime(sinceUtc);
        var newest = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var response = await client.GetAsync(
            $"{ApiBaseUrl}/athlete/0/activities?oldest={oldest:yyyy-MM-dd}&newest={newest:yyyy-MM-dd}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException($"intervals.icu API vrátilo chybu {(int)response.StatusCode} při načítání aktivit.");
        }

        // Parsed via JsonDocument (not a direct Deserialize<List<IntervalsIcuActivity>>) so each
        // element's exact original text is available for ExternalActivity.RawPayloadJson — a
        // lossless capture of whatever this DTO doesn't explicitly model, see docs/integrations-research.md §5.
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(rawJson);

        var result = new List<ExternalActivity>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var a = element.Deserialize<IntervalsIcuActivity>();
            if (a is null)
            {
                continue;
            }

            // Confirmed live 2026-09-17: intervals.icu deliberately withholds full data for
            // activities it synced from the athlete's own connected Strava account (source ==
            // "STRAVA") — only a stub with an explanatory "_note" comes back, everything else is
            // null. Not a parsing gap to work around: it's a real platform restriction (almost
            // certainly Strava's own API terms forbidding redistribution of Strava data to a
            // third-party API consumer like this app). Skip rather than create an empty activity —
            // the athlete needs a direct Strava connection (already supported) for these.
            if (string.Equals(a.Source, "STRAVA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Neither timestamp field present — nothing usable to store an activity against, so
            // skip rather than guess a start time.
            var startedAt = a.StartDateUtc ?? a.StartDateLocal;
            if (startedAt is null)
            {
                continue;
            }

            var durationSeconds = a.MovingTimeSeconds ?? 0;
            var paceSecondsPerKm = a.DistanceMeters is > 0 && durationSeconds > 0
                ? (int)Math.Round(durationSeconds * 1000m / a.DistanceMeters.Value)
                : (int?)null;

            var additionalMetrics = new List<ExternalActivityMetric>();
            if (a.ElapsedTimeSeconds is { } elapsed)
            {
                additionalMetrics.Add(new ExternalActivityMetric(ActivityMetricType.ElapsedTimeSeconds, elapsed, "s"));
            }
            if (a.TrainingLoad is { } trainingLoad)
            {
                additionalMetrics.Add(new ExternalActivityMetric(ActivityMetricType.TrainingLoad, (decimal)trainingLoad, "load"));
            }
            if (a.Intensity is { } intensity)
            {
                additionalMetrics.Add(new ExternalActivityMetric(ActivityMetricType.Intensity, (decimal)intensity, "%"));
            }
            if (a.Joules is { } joules)
            {
                additionalMetrics.Add(new ExternalActivityMetric(ActivityMetricType.WorkJoules, (decimal)joules, "J"));
            }
            if (a.WeightedAverageWatts is { } weightedWatts)
            {
                additionalMetrics.Add(new ExternalActivityMetric(ActivityMetricType.WeightedAveragePowerWatts, (decimal)weightedWatts, "W"));
            }

            result.Add(new ExternalActivity(
                ExternalId: a.Id,
                Sport: MapSport(a.Type),
                Title: a.Name,
                StartedAtUtc: DateTime.SpecifyKind(startedAt.Value, DateTimeKind.Utc),
                DurationSeconds: durationSeconds,
                DistanceMeters: a.DistanceMeters,
                ElevationGainMeters: a.TotalElevationGainMeters,
                AverageHeartRateBpm: a.AverageHeartRate.HasValue ? (int)Math.Round(a.AverageHeartRate.Value) : null,
                MaxHeartRateBpm: a.MaxHeartRate.HasValue ? (int)Math.Round(a.MaxHeartRate.Value) : null,
                AdditionalMetrics: additionalMetrics.Count > 0 ? additionalMetrics : null,
                RawPayloadJson: element.GetRawText(),
                AveragePaceSecondsPerKm: paceSecondsPerKm,
                AveragePowerWatts: a.AverageWatts.HasValue ? (int)Math.Round(a.AverageWatts.Value) : null,
                Calories: a.Calories.HasValue ? (int)Math.Round(a.Calories.Value) : null,
                DeviceName: a.DeviceName));
        }

        return result;
    }

    public async Task<ExternalActivityStreams?> FetchActivityStreamsAsync(string accessToken, string externalActivityId, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);
        var response = await client.GetAsync($"{ApiBaseUrl}/activity/{externalActivityId}/streams?types={StreamTypes}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException($"intervals.icu API vrátilo chybu {(int)response.StatusCode} při načítání detailu aktivity.");
        }

        List<IntervalsIcuStreamEntry> entries;
        try
        {
            entries = await response.Content.ReadFromJsonAsync<List<IntervalsIcuStreamEntry>>(cancellationToken: cancellationToken) ?? [];
        }
        catch (JsonException)
        {
            // See IntervalsIcuStreamEntry remarks: this response shape is community-sourced, not
            // officially confirmed. If it doesn't match, treat it as "no streams" rather than
            // failing the caller (e.g. the whole activity-detail view).
            return null;
        }

        var byType = entries
            .Where(e => !string.IsNullOrEmpty(e.Type))
            .ToDictionary(e => e.Type, e => (IReadOnlyList<double?>)e.Data, StringComparer.OrdinalIgnoreCase);

        if (!byType.TryGetValue("time", out var time) || time.Count == 0)
        {
            return null;
        }

        return new ExternalActivityStreams(
            TimeOffsetsSeconds: time.Select(v => (int)(v ?? 0)).ToList(),
            HeartRateBpm: byType.GetValueOrDefault("heartrate"),
            WattsOutput: byType.GetValueOrDefault("watts"),
            CadenceRpm: byType.GetValueOrDefault("cadence"),
            DistanceMeters: byType.GetValueOrDefault("distance"),
            AltitudeMeters: byType.GetValueOrDefault("altitude"),
            VelocityMetersPerSecond: byType.GetValueOrDefault("velocity_smooth"),
            GradePercent: byType.GetValueOrDefault("grade_smooth"));
    }

    /// <summary>Largest original activity file accepted — real FIT files are ~100 kB, multi-day ones a few MB.</summary>
    private const long MaxActivityFileBytes = 64L * 1024 * 1024;

    /// <summary>
    /// The original device file (<c>GET /activity/{id}/file</c> — FIT/TCX/GPX, possibly gzipped),
    /// verified against intervals.icu's OpenAPI spec (2026-09-30). Documented as unsupported for
    /// Strava-origin activities, which FetchRecentActivitiesAsync already skips.
    /// </summary>
    public async Task<byte[]?> DownloadActivityFileAsync(string accessToken, string externalActivityId, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);
        using var response = await client.GetAsync(
            $"{ApiBaseUrl}/activity/{Uri.EscapeDataString(externalActivityId)}/file", HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if ((int)response.StatusCode == 429)
        {
            throw new ProviderRateLimitedException("intervals.icu: vyčerpán limit požadavků API.");
        }
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity or HttpStatusCode.NoContent)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException($"intervals.icu API vrátilo chybu {(int)response.StatusCode} při stahování souboru aktivity.");
        }
        if (response.Content.Headers.ContentLength > MaxActivityFileBytes)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxActivityFileBytes)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    public async Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);

        var oldest = DateOnly.FromDateTime(sinceUtc);
        var newest = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.GetAsync(
            $"{ApiBaseUrl}/athlete/0/wellness?oldest={oldest:yyyy-MM-dd}&newest={newest:yyyy-MM-dd}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException($"intervals.icu API vrátilo chybu {(int)response.StatusCode} při načítání wellness dat.");
        }

        var entries = await response.Content.ReadFromJsonAsync<List<IntervalsIcuWellnessEntry>>(cancellationToken: cancellationToken) ?? [];

        var samples = new List<ExternalWellnessSample>();
        foreach (var entry in entries)
        {
            if (!DateOnly.TryParse(entry.Id, out var date))
            {
                continue;
            }

            samples.Add(new ExternalWellnessSample(
                date,
                HrvRmssdMs: entry.Hrv,
                RestingHeartRateBpm: entry.RestingHeartRate,
                ReadinessScore: entry.Readiness,
                StressScore: entry.Stress,
                SleepDurationMinutes: entry.SleepSeconds.HasValue ? entry.SleepSeconds.Value / 60 : null,
                HrvSdnnMs: entry.HrvSdnn,
                SleepScore: entry.SleepScore,
                AvgSleepingHeartRateBpm: entry.AvgSleepingHeartRate,
                WeightKg: entry.WeightKg,
                MoodScore: entry.Mood,
                SorenessScore: entry.Soreness,
                MotivationScore: entry.Motivation,
                Steps: entry.Steps,
                SpO2Percent: entry.SpO2,
                Vo2Max: entry.Vo2Max,
                HasInjurySignal: entry.Injury,
                Ctl: entry.Ctl,
                Atl: entry.Atl,
                RampRate: entry.RampRate));
        }

        return samples;
    }

    /// <summary>Confirmed on the intervals.icu community forum ("Intervals.icu OAuth support"):
    /// <c>DELETE /api/v1/disconnect-app</c> revokes the calling app's access token.</summary>
    public async Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);
        var response = await client.DeleteAsync($"{ApiBaseUrl}/disconnect-app", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("intervals.icu disconnect-app vrátilo {StatusCode} — token možná zůstává platný na straně intervals.icu.", (int)response.StatusCode);
        }
    }

    public string SettingsSyncScope => "SETTINGS:WRITE";

    /// <summary>
    /// <c>PUT /athlete/0/sport-settings/Run?recalcHrZones=false</c> — a partial update: only the fields
    /// sent change. <c>hr_zones</c> are the zones' upper bounds; intervals.icu requires one name per bound
    /// and silently overwrites <c>max_hr</c> unless it equals the last bound. recalcHrZones=false keeps them
    /// from being rebuilt from its %LTHR model (verified live by the intervals-icu-mcp project,
    /// github.com/hhopke/intervals-icu-mcp PR #139, and on our account 2026-10-06). <c>threshold_pace</c>
    /// is a speed in m/s, like <c>workout_doc.threshold_pace</c>. See docs/integrations/garmin-workout-model.md §5.
    /// </summary>
    public async Task PushTrainingSettingsAsync(string accessToken, TrainingSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);
        var zones = update.HeartRateZones;
        var body = new IntervalsIcuSportSettingsUpdate(
            zones.Count > 0 ? zones.Select(z => z.MaxBpm).ToList() : null,
            zones.Count > 0 ? zones.Select(z => string.IsNullOrWhiteSpace(z.Name) ? $"Z{z.ZoneNumber}" : z.Name).ToList() : null,
            zones.Count > 0 ? zones[^1].MaxBpm : null,
            update.ThresholdPaceSecondsPerKm is > 0 and var pace ? Math.Round(1000.0 / pace, 4) : null);
        var response = await client.PutAsJsonAsync($"{ApiBaseUrl}/athlete/0/sport-settings/{ZoneSportType}?recalcHrZones=false", body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("intervals.icu sport-settings PUT vrátilo {StatusCode}: {Body}", (int)response.StatusCode, detail.Length > 300 ? detail[..300] : detail);
        throw new TrainingSettingsSyncException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "intervals.icu odmítlo zápis — athlete musí znovu připojit účet.",
            HttpStatusCode.NotFound => "Athlete nemá v intervals.icu nastavení pro běh.",
            _ => $"intervals.icu nastavení nepřijalo (HTTP {(int)response.StatusCode}).",
        });
    }

    public string WorkoutPushScope => "CALENDAR:WRITE";

    /// <summary>
    /// <c>POST /athlete/0/events/bulk?upsert=true</c> with one WORKOUT event whose <c>external_id</c> is the
    /// PeakForm workout id: creates it, or updates the entry our app created earlier (date, sport and
    /// structure included). intervals.icu parses the description into steps and uploads it to Garmin
    /// Connect within its ~7-day window if the athlete enabled "Upload planned workouts" there.
    /// </summary>
    public async Task<PlannedWorkoutPushResult?> UpsertWorkoutAsync(string accessToken, PlannedWorkout workout, CancellationToken cancellationToken = default)
    {
        if (IntervalsIcuWorkoutDescriptionBuilder.Build(workout) is not { } ev)
        {
            return null;
        }

        var client = CreateAuthorizedClient(accessToken);
        var body = new[]
        {
            new IntervalsIcuWorkoutEventUpsert("WORKOUT", $"{workout.Date:yyyy-MM-dd}T00:00:00", ev.Type, ev.Name, ev.Description, ev.Target,
                workout.Id.ToString(), ev.MovingTimeSeconds),
        };
        var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/athlete/0/events/bulk?upsert=true&upsertOnUid=false&updatePlanApplied=false", body, cancellationToken);
        await EnsureCalendarWriteSucceededAsync(response, cancellationToken);

        string? eventId = null;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() > 0
                && json.RootElement[0].TryGetProperty("id", out var id))
            {
                eventId = id.ToString(); // a number today; read as text either way
            }
        }
        catch (JsonException)
        {
            // The write succeeded; only the id for reference is missing — matching uses external_id anyway.
        }
        return new PlannedWorkoutPushResult(eventId, ev.Warnings.Select(w => w.ToString()).ToList());
    }

    /// <summary><c>PUT /athlete/0/events/bulk-delete</c> by <c>external_id</c>; entries that don't exist are ignored.</summary>
    public async Task RemoveWorkoutAsync(string accessToken, Guid plannedWorkoutId, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(accessToken);
        var response = await client.PutAsJsonAsync($"{ApiBaseUrl}/athlete/0/events/bulk-delete",
            new[] { new IntervalsIcuEventReference(plannedWorkoutId.ToString()) }, cancellationToken);
        await EnsureCalendarWriteSucceededAsync(response, cancellationToken);
    }

    private async Task EnsureCalendarWriteSucceededAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("intervals.icu events vrátilo {StatusCode}: {Body}", (int)response.StatusCode, detail.Length > 300 ? detail[..300] : detail);
        throw new WorkoutPushException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "intervals.icu odmítlo zápis do kalendáře — athlete musí znovu připojit účet.",
            _ => $"intervals.icu trénink nepřijalo (HTTP {(int)response.StatusCode}).",
        });
    }

    private HttpClient CreateAuthorizedClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(nameof(IntervalsIcuIntegrationProvider));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private void RequireConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new BusinessRuleException(
                "intervals.icu integrace není nakonfigurována — chybí Client Id/Secret. Na rozdíl od Strava vyžaduje intervals.icu " +
                "nejprve ruční schválení OAuth aplikace (formulář \"Request OAuth access\" na intervals.icu/settings/apps, viz " +
                "docs/integrations-research.md §5) — počítejte s prodlevou, ne s okamžitým self-serve.");
        }
    }

    private static async Task<ExternalTokenResult> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BusinessRuleException($"intervals.icu OAuth selhalo ({(int)response.StatusCode}): {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<IntervalsIcuTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new BusinessRuleException("intervals.icu vrátilo neočekávanou odpověď.");

        return new ExternalTokenResult(
            token.AccessToken,
            RefreshToken: null,
            ExpiresAtUtc: null,
            ExternalAccountId: token.Athlete?.Id,
            GrantedScope: token.Scope ?? Scopes);
    }

    private static SportType MapSport(string? intervalsIcuType) => intervalsIcuType?.ToLowerInvariant() switch
    {
        "run" or "trailrun" or "virtualrun" => SportType.Running,
        "ride" or "mountainbikeride" or "gravelride" or "virtualride" or "ebikeride" => SportType.Cycling,
        "swim" => SportType.Swimming,
        "weighttraining" or "workout" or "strengthtraining" or "crossfit" => SportType.Strength,
        _ => SportType.Other,
    };
}
