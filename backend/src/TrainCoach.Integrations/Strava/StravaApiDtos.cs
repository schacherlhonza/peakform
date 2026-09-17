using System.Text.Json.Serialization;

namespace TrainCoach.Integrations.Strava;

/// <summary>
/// Shapes match the official Strava API v3 reference (developers.strava.com), verified as part
/// of docs/integrations-research.md. Only the fields this app actually uses are mapped.
/// </summary>
public class StravaTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAtUnixSeconds { get; set; }

    [JsonPropertyName("athlete")]
    public StravaAthlete? Athlete { get; set; }
}

public class StravaAthlete
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public class StravaActivity
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sport_type")]
    public string? SportType { get; set; }

    [JsonPropertyName("start_date")]
    public DateTime StartDateUtc { get; set; }

    [JsonPropertyName("moving_time")]
    public int MovingTimeSeconds { get; set; }

    [JsonPropertyName("distance")]
    public decimal DistanceMeters { get; set; }

    [JsonPropertyName("total_elevation_gain")]
    public decimal TotalElevationGainMeters { get; set; }

    [JsonPropertyName("average_heartrate")]
    public double? AverageHeartRate { get; set; }

    [JsonPropertyName("max_heartrate")]
    public double? MaxHeartRate { get; set; }

    [JsonPropertyName("average_speed")]
    public decimal? AverageSpeedMetersPerSecond { get; set; }

    [JsonPropertyName("average_watts")]
    public double? AverageWatts { get; set; }

    [JsonPropertyName("calories")]
    public double? Calories { get; set; }
}

/// <summary>
/// Shape of one entry in the `/activities/{id}/streams?key_by_type=true` response — every stream
/// type this app requests (time, heartrate, watts, cadence, distance, altitude, velocity_smooth,
/// grade_smooth) is a flat numeric array, so a single shape covers all of them.
/// </summary>
public class StravaStreamSet
{
    [JsonPropertyName("data")]
    public List<double?> Data { get; set; } = [];
}
