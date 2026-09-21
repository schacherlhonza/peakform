using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainCoach.Integrations.IntervalsIcu;

/// <summary>
/// <see cref="IntervalsIcuActivity"/>'s fields are now confirmed against a real account's raw API
/// response (2026-09-17, see docs/integrations-research.md §5 "ZJIŠTĚNÍ"). Wellness/stream shapes
/// are still based on the intervals.icu API Integration Cookbook and community-verified field
/// names — the official Swagger UI at intervals.icu/api/v1/docs/swagger-ui-index.html returned
/// HTTP 500 during that research session, so those are the best-documented names available but
/// not independently confirmed. Treat unexpected/missing fields as "no data" rather than a hard
/// failure (see the defensive parsing in <see cref="IntervalsIcuIntegrationProvider"/>).
/// </summary>
public class IntervalsIcuTokenResponse
{
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    // Confirmed on the forum by intervals.icu staff: no refresh_token is ever issued — the OAuth
    // flow only hands out an access token. See IntervalsIcuIntegrationProvider.RefreshTokenAsync.
    [JsonPropertyName("athlete")]
    public IntervalsIcuAthlete? Athlete { get; set; }
}

public class IntervalsIcuAthlete
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Id { get; set; }
}

public class IntervalsIcuActivity
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Sport, e.g. "Run", "Ride", "WeightTraining" — same vocabulary Strava uses.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("start_date")]
    public DateTime? StartDateUtc { get; set; }

    /// <summary>Fallback used only when <see cref="StartDateUtc"/> is absent — not itself UTC, but
    /// treating it as such is a closer approximation than dropping the activity entirely.</summary>
    [JsonPropertyName("start_date_local")]
    public DateTime? StartDateLocal { get; set; }

    [JsonPropertyName("moving_time")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? MovingTimeSeconds { get; set; }

    [JsonPropertyName("elapsed_time")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? ElapsedTimeSeconds { get; set; }

    [JsonPropertyName("distance")]
    public decimal? DistanceMeters { get; set; }

    [JsonPropertyName("total_elevation_gain")]
    public decimal? TotalElevationGainMeters { get; set; }

    [JsonPropertyName("average_heartrate")]
    public double? AverageHeartRate { get; set; }

    [JsonPropertyName("max_heartrate")]
    public double? MaxHeartRate { get; set; }

    [JsonPropertyName("icu_average_watts")]
    public double? AverageWatts { get; set; }

    [JsonPropertyName("icu_weighted_avg_watts")]
    public double? WeightedAverageWatts { get; set; }

    [JsonPropertyName("icu_joules")]
    public double? Joules { get; set; }

    [JsonPropertyName("icu_training_load")]
    public double? TrainingLoad { get; set; }

    [JsonPropertyName("icu_intensity")]
    public double? Intensity { get; set; }

    [JsonPropertyName("calories")]
    public double? Calories { get; set; }

    /// <summary>
    /// Confirmed live 2026-09-17: for activities where this is "STRAVA" (i.e. synced into
    /// intervals.icu from the athlete's own connected Strava account), intervals.icu deliberately
    /// returns only a stub — id/athlete id/date/source/<see cref="Note"/> — with every other field
    /// null. This is a real platform restriction (Strava's own API terms forbid intervals.icu
    /// redistributing Strava activity data to a third-party API consumer like this app), not a
    /// parsing gap — see docs/integrations-research.md §5. <see cref="IntervalsIcuIntegrationProvider.FetchRecentActivitiesAsync"/>
    /// skips these entirely rather than creating an empty <c>CompletedActivity</c>.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Present (and explanatory) only on the Strava stub records described above.</summary>
    [JsonPropertyName("_note")]
    public string? Note { get; set; }

    /// <summary>
    /// e.g. "Garmin Forerunner 965" — needed to satisfy the intervals.icu API Terms and
    /// Conditions' Garmin-attribution requirement when Garmin-sourced data is displayed
    /// (docs/integrations-research.md §5, "Garmin attribution povinnost").
    /// </summary>
    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }
}

/// <summary>
/// One entry of the `/api/v1/activity/{id}/streams?types=...` response — community sources
/// describe this as a JSON array of per-type entries (unlike Strava's `key_by_type=true` object),
/// but this shape could not be confirmed against the live Swagger. See the try/catch around
/// deserialization in <see cref="IntervalsIcuIntegrationProvider.FetchActivityStreamsAsync"/>,
/// which treats a shape mismatch as "no streams available" instead of failing the whole sync.
/// </summary>
public class IntervalsIcuStreamEntry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<double?> Data { get; set; } = [];
}

/// <summary>
/// One day of `/api/v1/athlete/{id}/wellness/{date}` (or the bulk range variant). Field names
/// per the API Integration Cookbook and community field references (see class remarks on
/// <see cref="IntervalsIcuTokenResponse"/> re: verification status). Only the fields this app
/// maps into TrainCoach's own wellness model are declared — the endpoint returns more.
/// </summary>
public class IntervalsIcuWellnessEntry
{
    /// <summary>ISO-8601 local date, e.g. "2026-09-17".</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("restingHR")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? RestingHeartRate { get; set; }

    /// <summary>
    /// intervals.icu does not document a single fixed HRV algorithm per device — this is
    /// whatever the connected source (Garmin/Oura/WHOOP/...) reported as its daily HRV figure,
    /// passed through as-is. Treated as an rMSSD-shaped value for TrainCoach's own
    /// <c>HrvMeasurement.RmssdMs</c>, which is an approximation, not a verified equivalence.
    /// </summary>
    [JsonPropertyName("hrv")]
    public decimal? Hrv { get; set; }

    [JsonPropertyName("readiness")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Readiness { get; set; }

    [JsonPropertyName("stress")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Stress { get; set; }

    [JsonPropertyName("sleepSecs")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? SleepSeconds { get; set; }

    [JsonPropertyName("weight")]
    public decimal? WeightKg { get; set; }

    [JsonPropertyName("hrvSDNN")]
    public decimal? HrvSdnn { get; set; }

    /// <summary>Confirmed live 2026-09-18: intervals.icu sometimes reports this as a JSON float
    /// (e.g. 79.0) rather than a plain integer — System.Text.Json throws on that for a bare
    /// <c>int?</c> target, which previously took down the whole sync run. See
    /// <see cref="FlexibleInt32Converter"/>.</summary>
    [JsonPropertyName("sleepScore")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? SleepScore { get; set; }

    [JsonPropertyName("avgSleepingHR")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? AvgSleepingHeartRate { get; set; }

    [JsonPropertyName("mood")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Mood { get; set; }

    [JsonPropertyName("soreness")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Soreness { get; set; }

    [JsonPropertyName("motivation")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Motivation { get; set; }

    [JsonPropertyName("steps")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Steps { get; set; }

    [JsonPropertyName("spO2")]
    public decimal? SpO2 { get; set; }

    [JsonPropertyName("vo2max")]
    public decimal? Vo2Max { get; set; }

    [JsonPropertyName("ctl")]
    public decimal? Ctl { get; set; }

    [JsonPropertyName("atl")]
    public decimal? Atl { get; set; }

    [JsonPropertyName("rampRate")]
    public decimal? RampRate { get; set; }

    /// <summary>intervals.icu returns this as a boolean daily flag — deliberately not wired to
    /// auto-create/resolve a <c>PainOrHealthFlag</c>, see docs/integrations-research.md §5.</summary>
    [JsonPropertyName("injury")]
    public bool? Injury { get; set; }
}

/// <summary>
/// Reads an "id"-like field as a string regardless of whether the API sent it as a JSON string
/// or a JSON number — intervals.icu's exact numbering scheme for activity/athlete ids could not
/// be confirmed against the live Swagger (see docs/integrations-research.md §5), so this avoids
/// a hard `JsonException` if it turns out to be numeric rather than a string like Strava's.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for a string/number id field."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>
/// Reads an integer-shaped field even when intervals.icu sends it as a JSON float — confirmed
/// live 2026-09-18 for <c>sleepScore</c> (e.g. <c>79.0</c> instead of <c>79</c>), which
/// System.Text.Json's default handling rejects for a bare <c>int?</c> target
/// (<see cref="Utf8JsonReader.GetInt32"/> throws on any fractional-looking number token, even one
/// with no actual fractional value). Rounds rather than truncates so a genuinely fractional score
/// isn't silently biased downward.
/// </summary>
public class FlexibleInt32Converter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt32(out var i) ? i : (int)Math.Round(reader.GetDouble()),
            JsonTokenType.String => int.TryParse(reader.GetString(), out var parsed) ? parsed : null,
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for an integer field."),
        };

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is { } v) writer.WriteNumberValue(v);
        else writer.WriteNullValue();
    }
}
