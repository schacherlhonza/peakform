using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution.Streams;

public static class ActivityStreamMapping
{
    /// <summary>Distance over which grade is measured — per-sample grade from GPS/barometric
    /// altitude is pure noise.</summary>
    private const double GradeWindowMeters = 30;

    /// <summary>Below this the athlete is standing; a pace of "50 min/km" would only wreck the chart scale.</summary>
    private const double MinSpeedForPace = 0.5;

    public static ActivityStream ToEntity(Guid sourceRecordId, ActivityStreamData data, ActivityStreamOrigin origin, DateTime nowUtc)
    {
        var lat = data.Latitude?.Where(v => v.HasValue).Select(v => v!.Value).ToList() ?? [];
        var lon = data.Longitude?.Where(v => v.HasValue).Select(v => v!.Value).ToList() ?? [];
        var hasPosition = lat.Count > 0 && lon.Count > 0;
        var firstFix = hasPosition ? Enumerable.Range(0, data.Count).First(i => data.Latitude![i].HasValue && data.Longitude![i].HasValue) : -1;

        return new ActivityStream
        {
            ActivitySourceRecordId = sourceRecordId,
            Origin = origin,
            SampleCount = data.Count,
            OriginalSampleCount = data.OriginalSampleCount,
            Channels = data.Channels,
            StartLatitude = hasPosition ? data.Latitude![firstFix] : null,
            StartLongitude = hasPosition ? data.Longitude![firstFix] : null,
            MinLatitude = hasPosition ? lat.Min() : null,
            MaxLatitude = hasPosition ? lat.Max() : null,
            MinLongitude = hasPosition ? lon.Min() : null,
            MaxLongitude = hasPosition ? lon.Max() : null,
            FormatVersion = ActivityStreamCodec.CurrentFormatVersion,
            Payload = ActivityStreamCodec.Encode(data),
            CreatedAtUtc = nowUtc,
        };
    }

    public static ActivityStreamsDto ToDto(Guid activityId, SportType sport, ActivityStream stream)
    {
        var d = ActivityStreamCodec.Decode(stream.Payload, stream.FormatVersion, stream.OriginalSampleCount);
        return new ActivityStreamsDto(
            activityId,
            d.TimeOffsetsSeconds,
            d.HeartRateBpm,
            StepsPerMinute(sport, d.CadenceRpm),
            d.PowerWatts,
            d.DistanceMeters?.Select(v => v is { } x ? (decimal?)Math.Round((decimal)x, 1) : null).ToList(),
            d.AltitudeMeters?.Select(v => v is { } x ? (decimal?)Math.Round((decimal)x, 1) : null).ToList(),
            d.SpeedMetersPerSecond?.Select(v => v is > MinSpeedForPace ? (int?)Math.Round(1000 / v.Value) : null).ToList(),
            Grade(d.DistanceMeters, d.AltitudeMeters),
            d.Latitude,
            d.Longitude,
            d.TemperatureC?.Select(v => v is { } x ? (decimal?)Math.Round((decimal)x, 1) : null).ToList(),
            ActivityStreamSource.Stored,
            d.IsDownsampled);
    }

    /// <summary>FIT/GPX/TCX and the Strava API all record running cadence per leg (~85);
    /// runners think in steps per minute (~170).</summary>
    public static IReadOnlyList<int?>? StepsPerMinute(SportType sport, IReadOnlyList<int?>? cadence) =>
        sport == SportType.Running ? cadence?.Select(c => c * 2).ToList() : cadence;

    /// <summary>Grade at each sample over the trailing ~<see cref="GradeWindowMeters"/>: rise from
    /// the latest earlier sample at least that far back, divided by the distance between them.</summary>
    internal static IReadOnlyList<decimal?>? Grade(double?[]? distance, double?[]? altitude)
    {
        if (distance is null || altitude is null)
        {
            return null;
        }

        var valid = Enumerable.Range(0, distance.Length).Where(i => distance[i].HasValue && altitude[i].HasValue).ToArray();
        var result = new decimal?[distance.Length];
        var from = 0; // index into `valid`
        for (var v = 0; v < valid.Length; v++)
        {
            var i = valid[v];
            // Move the window start forward while the next candidate is still far enough back.
            while (from + 1 < v && distance[i]!.Value - distance[valid[from + 1]]!.Value >= GradeWindowMeters)
            {
                from++;
            }
            var j = valid[from];
            var run = distance[i]!.Value - distance[j]!.Value;
            if (run >= GradeWindowMeters)
            {
                result[i] = Math.Round((decimal)((altitude[i]!.Value - altitude[j]!.Value) / run * 100), 1);
            }
        }
        return result;
    }
}
