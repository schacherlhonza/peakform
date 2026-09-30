namespace TrainCoach.Application.Execution.Streams;

/// <summary>
/// Reduces a stream to at most <see cref="DefaultMaxPoints"/> index-aligned samples — enough for
/// charts and the route map while cutting storage roughly 3× for typical 1 Hz recordings.
/// <list type="bullet">
/// <item>The activity's time span is cut into equal buckets; each non-empty bucket yields one
/// sample. Pauses therefore stay visible as gaps in time instead of being bridged.</item>
/// <item>Effort channels (heart rate, power, cadence, speed) are averaged over the bucket so short
/// spikes still shape the curve; position/altitude/distance are taken from the one sample
/// nearest the bucket's middle, so the route never cuts corners through averaged coordinates.</item>
/// <item>The first and last sample are always kept.</item>
/// </list>
/// </summary>
public static class ActivityStreamDownsampler
{
    public const int DefaultMaxPoints = 2000;

    public static ActivityStreamData Downsample(ActivityStreamData data, int maxPoints = DefaultMaxPoints)
    {
        var n = data.Count;
        if (n <= maxPoints)
        {
            return data;
        }

        var time = data.TimeOffsetsSeconds;
        var span = Math.Max(1, time[^1] - time[0]);
        var bucketCount = maxPoints - 2; // first and last are kept separately
        var bucketWidth = (double)span / bucketCount;

        // Bucket ranges over sample indexes (samples are time-sorted).
        var ranges = new List<(int From, int To, int Pick)>(maxPoints) { (0, 0, 0) };
        var i = 1;
        for (var b = 0; b < bucketCount && i < n - 1; b++)
        {
            var bucketEnd = time[0] + (b + 1) * bucketWidth;
            var from = i;
            while (i < n - 1 && time[i] < bucketEnd)
            {
                i++;
            }
            if (i == from)
            {
                continue; // empty bucket — a pause
            }
            var middle = time[0] + (b + 0.5) * bucketWidth;
            var pick = from;
            for (var k = from + 1; k < i; k++)
            {
                if (Math.Abs(time[k] - middle) < Math.Abs(time[pick] - middle))
                {
                    pick = k;
                }
            }
            ranges.Add((from, i - 1, pick));
        }
        ranges.Add((n - 1, n - 1, n - 1));

        return new ActivityStreamData
        {
            TimeOffsetsSeconds = ranges.Select(r => time[r.Pick]).ToArray(),
            HeartRateBpm = AverageInt(data.HeartRateBpm, ranges),
            PowerWatts = AverageInt(data.PowerWatts, ranges),
            CadenceRpm = AverageInt(data.CadenceRpm, ranges),
            SpeedMetersPerSecond = AverageDouble(data.SpeedMetersPerSecond, ranges),
            TemperatureC = AverageDouble(data.TemperatureC, ranges),
            DistanceMeters = PickNearest(data.DistanceMeters, ranges),
            AltitudeMeters = PickNearest(data.AltitudeMeters, ranges),
            Latitude = PickPosition(data.Latitude, data.Longitude, ranges, latitude: true),
            Longitude = PickPosition(data.Latitude, data.Longitude, ranges, latitude: false),
            OriginalSampleCount = data.OriginalSampleCount,
        };
    }

    private static int?[]? AverageInt(int?[]? values, List<(int From, int To, int Pick)> ranges)
    {
        if (values is null)
        {
            return null;
        }
        return ranges.Select(r =>
        {
            long sum = 0;
            var count = 0;
            for (var k = r.From; k <= r.To; k++)
            {
                if (values[k] is { } v)
                {
                    sum += v;
                    count++;
                }
            }
            return count == 0 ? (int?)null : (int)Math.Round((double)sum / count);
        }).ToArray();
    }

    private static double?[]? AverageDouble(double?[]? values, List<(int From, int To, int Pick)> ranges)
    {
        if (values is null)
        {
            return null;
        }
        return ranges.Select(r =>
        {
            double sum = 0;
            var count = 0;
            for (var k = r.From; k <= r.To; k++)
            {
                if (values[k] is { } v)
                {
                    sum += v;
                    count++;
                }
            }
            return count == 0 ? (double?)null : sum / count;
        }).ToArray();
    }

    /// <summary>The picked sample's value, or — if that one is missing — the nearest present one in the bucket.</summary>
    private static double?[]? PickNearest(double?[]? values, List<(int From, int To, int Pick)> ranges)
    {
        if (values is null)
        {
            return null;
        }
        return ranges.Select(r => NearestIndex(k => values[k].HasValue, r) is { } k ? values[k] : null).ToArray();
    }

    /// <summary>Latitude and longitude must come from the same sample.</summary>
    private static double?[]? PickPosition(double?[]? lat, double?[]? lon, List<(int From, int To, int Pick)> ranges, bool latitude)
    {
        if (lat is null || lon is null)
        {
            return null;
        }
        return ranges.Select(r => NearestIndex(k => lat[k].HasValue && lon[k].HasValue, r) is { } k ? (latitude ? lat[k] : lon[k]) : null).ToArray();
    }

    private static int? NearestIndex(Func<int, bool> hasValue, (int From, int To, int Pick) r)
    {
        for (var offset = 0; offset <= r.To - r.From; offset++)
        {
            if (r.Pick - offset >= r.From && hasValue(r.Pick - offset))
            {
                return r.Pick - offset;
            }
            if (r.Pick + offset <= r.To && hasValue(r.Pick + offset))
            {
                return r.Pick + offset;
            }
        }
        return null;
    }
}
