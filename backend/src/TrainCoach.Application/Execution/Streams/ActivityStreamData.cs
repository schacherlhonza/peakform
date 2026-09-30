using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Execution.Streams;

/// <summary>
/// One activity's detail stream in columnar form: every channel array is either null (not
/// recorded) or exactly <see cref="Count"/> long, index-aligned with <see cref="TimeOffsetsSeconds"/>
/// — so a chart hover at index i maps straight to the map position at index i.
/// </summary>
public sealed class ActivityStreamData
{
    public required int[] TimeOffsetsSeconds { get; init; }
    public int?[]? HeartRateBpm { get; init; }
    public int?[]? PowerWatts { get; init; }
    public int?[]? CadenceRpm { get; init; }
    public double?[]? DistanceMeters { get; init; }
    public double?[]? AltitudeMeters { get; init; }
    public double?[]? SpeedMetersPerSecond { get; init; }
    public double?[]? Latitude { get; init; }
    public double?[]? Longitude { get; init; }
    public double?[]? TemperatureC { get; init; }

    /// <summary>Samples in the source before downsampling (equals <see cref="Count"/> if none happened).</summary>
    public int OriginalSampleCount { get; init; }

    public int Count => TimeOffsetsSeconds.Length;
    public bool IsDownsampled => OriginalSampleCount > Count;

    public ActivityStreamChannels Channels =>
        (HasAny(HeartRateBpm) ? ActivityStreamChannels.HeartRate : 0)
        | (HasAny(PowerWatts) ? ActivityStreamChannels.Power : 0)
        | (HasAny(CadenceRpm) ? ActivityStreamChannels.Cadence : 0)
        | (HasAny(DistanceMeters) ? ActivityStreamChannels.Distance : 0)
        | (HasAny(AltitudeMeters) ? ActivityStreamChannels.Altitude : 0)
        | (HasAny(SpeedMetersPerSecond) ? ActivityStreamChannels.Speed : 0)
        | (HasAny(Latitude) && HasAny(Longitude) ? ActivityStreamChannels.Position : 0)
        | (HasAny(TemperatureC) ? ActivityStreamChannels.Temperature : 0);

    private static bool HasAny<T>(T?[]? values) where T : struct => values is not null && values.Any(v => v.HasValue);
}

/// <summary>
/// Accumulates raw samples while a file is being parsed (any order of channels, gaps allowed),
/// then produces a time-sorted <see cref="ActivityStreamData"/> with derived channels filled in
/// (speed from distance/time, distance from GPS) and empty channels dropped.
/// </summary>
public sealed class ActivityStreamBuilder
{
    private readonly List<Sample> _samples = [];

    public record struct Sample(
        DateTime TimestampUtc, int? HeartRate = null, int? Power = null, int? Cadence = null, double? Distance = null,
        double? Altitude = null, double? Speed = null, double? Latitude = null, double? Longitude = null, double? Temperature = null);

    public int Count => _samples.Count;

    public void Add(Sample sample) => _samples.Add(sample);

    public ActivityStreamData? Build()
    {
        if (_samples.Count < 2)
        {
            return null;
        }

        // Sorted, and one sample per second at most (some devices log duplicate timestamps).
        var sorted = _samples.OrderBy(s => s.TimestampUtc).ToList();
        var samples = sorted
            .GroupBy(s => (long)(s.TimestampUtc - sorted[0].TimestampUtc).TotalSeconds)
            .Select(g => g.First())
            .ToList();
        var start = samples[0].TimestampUtc;
        var n = samples.Count;

        var time = new int[n];
        for (var i = 0; i < n; i++)
        {
            time[i] = (int)Math.Round((samples[i].TimestampUtc - start).TotalSeconds);
        }

        var lat = samples.Select(s => ValidCoordinate(s.Latitude, 90)).ToArray();
        var lon = samples.Select(s => ValidCoordinate(s.Longitude, 180)).ToArray();
        // (0, 0) is how several devices encode "no fix".
        for (var i = 0; i < n; i++)
        {
            if (lat[i] is 0 && lon[i] is 0)
            {
                lat[i] = lon[i] = null;
            }
        }

        var distance = samples.Select(s => s.Distance).ToArray();
        if (!distance.Any(d => d.HasValue) && lat.Any(l => l.HasValue))
        {
            distance = DistanceFromPositions(lat, lon);
        }

        var speed = samples.Select(s => s.Speed).ToArray();
        if (!speed.Any(v => v.HasValue) && distance.Any(d => d.HasValue))
        {
            speed = SpeedFromDistance(time, distance);
        }

        return new ActivityStreamData
        {
            TimeOffsetsSeconds = time,
            HeartRateBpm = NullIfEmpty(samples.Select(s => s.HeartRate is > 0 ? s.HeartRate : null).ToArray()),
            PowerWatts = NullIfEmpty(samples.Select(s => s.Power).ToArray()),
            CadenceRpm = NullIfEmpty(samples.Select(s => s.Cadence).ToArray()),
            DistanceMeters = NullIfEmpty(distance),
            AltitudeMeters = NullIfEmpty(samples.Select(s => s.Altitude).ToArray()),
            SpeedMetersPerSecond = NullIfEmpty(speed),
            Latitude = NullIfEmpty(lat),
            Longitude = NullIfEmpty(lon),
            TemperatureC = NullIfEmpty(samples.Select(s => s.Temperature).ToArray()),
            OriginalSampleCount = n,
        };
    }

    private static double? ValidCoordinate(double? value, double limit) =>
        value is { } v && double.IsFinite(v) && Math.Abs(v) <= limit ? v : null;

    private static T?[]? NullIfEmpty<T>(T?[] values) where T : struct => values.Any(v => v.HasValue) ? values : null;

    internal static double?[] DistanceFromPositions(double?[] lat, double?[] lon)
    {
        var result = new double?[lat.Length];
        double total = 0;
        int? previous = null;
        for (var i = 0; i < lat.Length; i++)
        {
            if (lat[i] is null || lon[i] is null)
            {
                continue;
            }
            if (previous is { } p)
            {
                total += Haversine(lat[p]!.Value, lon[p]!.Value, lat[i]!.Value, lon[i]!.Value);
            }
            result[i] = total;
            previous = i;
        }
        return result;
    }

    private static double?[] SpeedFromDistance(int[] time, double?[] distance)
    {
        var result = new double?[time.Length];
        int? previous = null;
        for (var i = 0; i < time.Length; i++)
        {
            if (distance[i] is null)
            {
                continue;
            }
            if (previous is { } p && time[i] > time[p])
            {
                result[i] = Math.Max(0, (distance[i]!.Value - distance[p]!.Value) / (time[i] - time[p]));
            }
            previous = i;
        }
        return result;
    }

    /// <summary>Great-circle distance in meters.</summary>
    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earthRadius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
