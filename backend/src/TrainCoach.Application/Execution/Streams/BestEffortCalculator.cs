using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Execution.Streams;

public record BestEffortResult(BestEffortType Type, decimal Value, int StartOffsetSeconds);

/// <summary>
/// Best efforts inside one activity stream:
/// <list type="bullet">
/// <item><b>Distance</b> (runs only): fastest time over 1 km … marathon. A two-pointer window over
/// the distance channel; the effort's start is interpolated between the two samples around it, so
/// the result doesn't depend on where samples happen to fall. GPS glitches are removed first; an
/// increment is kept only if BOTH hold: the speed over the trailing <see cref="GlitchWindowSeconds"/>
/// stays under <see cref="MaxRunSegmentSpeed"/>, and the increment divided by the time since the
/// distance last grew stays under <see cref="MaxCatchUpSpeed"/>. Why two: after a signal loss the
/// distance stalls and then catches up in one big step — real distance (a 2019 marathon: 356 m
/// after a 39 s stall) — while wandering GPS jumps in full motion (23 jumps up to 144 m/s in one
/// run, giving a "2:31 km"). Measured over the whole archive, real catch-ups ran 1–3 m/s over
/// their stall (max 9). Efforts faster than <see cref="MaxPlausibleRunSpeed"/> are still rejected.</item>
/// <item><b>Power</b> (any sport with power — cycling, Garmin running power): best average over
/// 1, 5 and 20 minutes on a per-second step-hold series. A window may not span a pause (a gap
/// between samples longer than <see cref="MaxSampleGapSeconds"/>). Samples above a physiological
/// ceiling (running 1 000 W, otherwise 2 500 W) are sensor dropouts and ignored — real data had
/// running power spikes to 3 664 W, giving a "2 479 W minute".</item>
/// </list>
/// Bump <see cref="Version"/> when the algorithm changes: stored efforts of older versions are
/// recomputed by the background pass.
/// </summary>
public static class BestEffortCalculator
{
    public const int Version = 1;
    public const int MaxSampleGapSeconds = 30;

    /// <summary>~2:23 /km — faster than any amateur's best 1 km; anything above is a GPS jump.</summary>
    public const double MaxPlausibleRunSpeed = 7.0;

    /// <summary>Between two samples a runner never covers ground faster than this; more is a GPS jump.</summary>
    public const double MaxRunSegmentSpeed = 8.0;

    /// <summary>Span over which <see cref="MaxRunSegmentSpeed"/> is judged.</summary>
    public const int GlitchWindowSeconds = 60;

    /// <summary>An increment over the time since the distance last grew may not exceed this.</summary>
    public const double MaxCatchUpSpeed = 10.0;

    private const int MaxRunningWatts = 1000;
    private const int MaxOtherWatts = 2500;
    private const int MaxSeriesSeconds = 48 * 3600;

    private static readonly (BestEffortType Type, double Meters)[] Distances =
    [
        (BestEffortType.Distance1Km, 1000), (BestEffortType.Distance5Km, 5000), (BestEffortType.Distance10Km, 10000),
        (BestEffortType.DistanceHalfMarathon, 21097.5), (BestEffortType.DistanceMarathon, 42195),
    ];

    private static readonly (BestEffortType Type, int Seconds)[] PowerWindows =
    [
        (BestEffortType.Power1Min, 60), (BestEffortType.Power5Min, 300), (BestEffortType.Power20Min, 1200),
    ];

    public static IReadOnlyList<BestEffortResult> Compute(SportType sport, ActivityStreamData data)
    {
        var results = new List<BestEffortResult>();
        if (sport == SportType.Running && data.DistanceMeters is { } distance)
        {
            results.AddRange(DistanceEfforts(data.TimeOffsetsSeconds, distance));
        }
        if (data.PowerWatts is { } power)
        {
            results.AddRange(PowerEfforts(data.TimeOffsetsSeconds, power, sport == SportType.Running ? MaxRunningWatts : MaxOtherWatts));
        }
        return results;
    }

    internal static IEnumerable<BestEffortResult> DistanceEfforts(int[] time, double?[] distance)
    {
        var t = new List<double>();
        var d = new List<double>();
        double? previousRaw = null;
        var windowStart = 0; // first kept sample within GlitchWindowSeconds of the current one
        double lastGrowth = 0; // time the kept distance last increased
        for (var k = 0; k < time.Length && k < distance.Length; k++)
        {
            if (distance[k] is not { } raw)
            {
                continue;
            }
            if (d.Count == 0)
            {
                t.Add(time[k]);
                d.Add(0);
                previousRaw = raw;
                lastGrowth = time[k];
                continue;
            }
            // Distance only grows. A glitch increment is dropped and the rest of the track shifts
            // down by it instead of jumping ahead.
            var increment = Math.Max(0, raw - previousRaw!.Value);
            previousRaw = raw;
            if (time[k] <= t[^1])
            {
                continue;
            }
            while (windowStart < t.Count - 1 && time[k] - t[windowStart + 1] >= GlitchWindowSeconds)
            {
                windowStart++;
            }
            var span = Math.Max(time[k] - t[windowStart], 1);
            var sinceGrowth = Math.Max(time[k] - lastGrowth, 1);
            if ((d[^1] + increment - d[windowStart]) / span > MaxRunSegmentSpeed || increment / sinceGrowth > MaxCatchUpSpeed)
            {
                continue;
            }
            t.Add(time[k]);
            d.Add(d[^1] + increment);
            if (increment > 0)
            {
                lastGrowth = time[k];
            }
        }
        if (d.Count < 2)
        {
            yield break;
        }

        foreach (var (type, meters) in Distances)
        {
            if (d[^1] - d[0] < meters)
            {
                continue;
            }

            double? best = null;
            double bestStart = 0;
            var i = 0;
            for (var j = 1; j < d.Count; j++)
            {
                while (i + 1 < j && d[j] - d[i + 1] >= meters)
                {
                    i++;
                }
                if (d[j] - d[i] < meters)
                {
                    continue;
                }
                // The point exactly `meters` before sample j lies between samples i and i+1.
                var span = d[i + 1] - d[i];
                var fraction = span > 0 ? (d[j] - meters - d[i]) / span : 0;
                var start = t[i] + fraction * (t[i + 1] - t[i]);
                var seconds = t[j] - start;
                if (seconds <= 0 || meters / seconds > MaxPlausibleRunSpeed)
                {
                    continue;
                }
                if (best is null || seconds < best)
                {
                    best = seconds;
                    bestStart = start;
                }
            }

            if (best is { } b)
            {
                yield return new BestEffortResult(type, Math.Round((decimal)b, 1), (int)Math.Round(bestStart));
            }
        }
    }

    internal static IEnumerable<BestEffortResult> PowerEfforts(int[] time, int?[] power, int maxWatts = MaxOtherWatts)
    {
        var samples = Enumerable.Range(0, Math.Min(time.Length, power.Length))
            .Where(k => power[k] is { } w && w <= maxWatts)
            .Select(k => (T: time[k], P: power[k]!.Value))
            .ToList();
        if (samples.Count < 2)
        {
            yield break;
        }

        var t0 = samples[0].T;
        var length = Math.Min(samples[^1].T - t0, MaxSeriesSeconds);
        if (length < PowerWindows[0].Seconds)
        {
            yield break;
        }

        // Per-second step-hold series; seconds inside a pause stay "missing".
        var values = new int[length];
        var missing = new bool[length];
        Array.Fill(missing, true);
        for (var k = 0; k + 1 < samples.Count; k++)
        {
            var from = samples[k].T - t0;
            var to = Math.Min(samples[k + 1].T - t0, length);
            if (to - from > MaxSampleGapSeconds)
            {
                continue;
            }
            for (var s = from; s < to; s++)
            {
                values[s] = samples[k].P;
                missing[s] = false;
            }
        }

        var sum = new long[length + 1];
        var gaps = new int[length + 1];
        for (var s = 0; s < length; s++)
        {
            sum[s + 1] = sum[s] + values[s];
            gaps[s + 1] = gaps[s] + (missing[s] ? 1 : 0);
        }

        foreach (var (type, window) in PowerWindows)
        {
            double? best = null;
            var bestStart = 0;
            for (var start = 0; start + window <= length; start++)
            {
                if (gaps[start + window] - gaps[start] > 0)
                {
                    continue;
                }
                var average = (double)(sum[start + window] - sum[start]) / window;
                if (best is null || average > best)
                {
                    best = average;
                    bestStart = start;
                }
            }
            if (best is { } b && b > 0)
            {
                yield return new BestEffortResult(type, Math.Round((decimal)b, 1), bestStart + t0);
            }
        }
    }
}
