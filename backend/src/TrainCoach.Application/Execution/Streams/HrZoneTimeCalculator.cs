namespace TrainCoach.Application.Execution.Streams;

public record HrZoneBounds(int ZoneNumber, int MinBpm, int MaxBpm);

/// <summary>
/// Seconds spent in each heart rate zone, over the same time the activity's duration shows:
/// <list type="bullet">
/// <item>Each sample's heart rate holds until the next sample (a step function — the stored
/// stream is downsampled, samples are 1-9 s apart), except across a gap longer than
/// <see cref="MaxSampleGapSeconds"/>: that's a pause.</item>
/// <item>With a speed channel, only <b>moving</b> time counts (speed above
/// <see cref="MovingSpeedMetersPerSecond"/>) — the displayed duration is moving time, and a
/// watch left recording while standing otherwise adds the standing to the zones (a 30-minute walk
/// inside a 76-minute recording). Without speed (strength, indoor) every non-gap second counts.</item>
/// <item>A rate below the lowest zone counts as <see cref="BelowZones"/>, not as zone 1 — an
/// athlete whose zone 1 starts at 130 bpm walking at 84 bpm isn't in any zone. Above the top
/// zone counts in the top zone; in a hole between two zones' ranges, in the zone below it.</item>
/// </list>
/// </summary>
public static class HrZoneTimeCalculator
{
    public const int MaxSampleGapSeconds = 30;
    public const double MovingSpeedMetersPerSecond = 0.5;

    /// <summary>Key for time below the lowest zone.</summary>
    public const int BelowZones = 0;

    /// <returns>Zone number (or <see cref="BelowZones"/>) → seconds; empty when there's no heart rate or no zones.</returns>
    public static IReadOnlyDictionary<int, int> Compute(
        IReadOnlyList<int> timeOffsetsSeconds, IReadOnlyList<int?>? heartRate, IReadOnlyList<HrZoneBounds> zones,
        IReadOnlyList<double?>? speedMetersPerSecond = null)
    {
        var result = new Dictionary<int, int>();
        if (heartRate is null || zones.Count == 0)
        {
            return result;
        }

        var ordered = zones.OrderBy(z => z.MinBpm).ToList();
        var useSpeed = speedMetersPerSecond is not null && speedMetersPerSecond.Any(v => v.HasValue);
        for (var i = 0; i + 1 < timeOffsetsSeconds.Count && i < heartRate.Count; i++)
        {
            if (heartRate[i] is not { } bpm)
            {
                continue;
            }
            var dt = timeOffsetsSeconds[i + 1] - timeOffsetsSeconds[i];
            if (dt <= 0 || dt > MaxSampleGapSeconds)
            {
                continue;
            }
            if (useSpeed && !(i < speedMetersPerSecond!.Count && speedMetersPerSecond[i] is > MovingSpeedMetersPerSecond))
            {
                continue;
            }
            var zone = ZoneFor(bpm, ordered);
            result[zone] = result.GetValueOrDefault(zone) + dt;
        }
        return result;
    }

    private static int ZoneFor(int bpm, List<HrZoneBounds> ordered)
    {
        var zone = BelowZones;
        foreach (var z in ordered)
        {
            if (bpm >= z.MinBpm)
            {
                zone = z.ZoneNumber;
            }
        }
        return zone;
    }
}
