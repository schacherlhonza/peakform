namespace TrainCoach.Application.Wellness;

public enum HealthMetric
{
    Hrv = 1,
    RestingHeartRate = 2,
    SleepDuration = 3,
    SleepScore = 4,
    AvgSleepingHeartRate = 5,
    SpO2 = 6,
}

public enum HealthMetricStatus
{
    InRange = 1,
    AboveRange = 2,
    BelowRange = 3,

    /// <summary>A value exists but there's too little history for a personal range yet.</summary>
    NoRange = 4,

    NoData = 5,
}

/// <param name="IsConcerning">True only when the value is out of range in the direction that
/// signals strain (HRV/sleep/SpO2 low, resting/sleeping HR high) — e.g. HRV above range is
/// flagged but not alarming.</param>
public record HealthMetricResult(HealthMetric Metric, decimal? Value, decimal? RangeLow, decimal? RangeHigh, HealthMetricStatus Status, bool IsConcerning);

/// <summary>
/// Garmin-style "health status": each overnight metric against the athlete's own normal range —
/// mean ± 1.5 SD of the preceding 28 days, never including the day itself. Complements
/// <see cref="ReadinessCalculator"/>: this answers "is my body behaving normally", readiness
/// answers "how ready am I to train" (and also weighs training load). See
/// docs/wellness/readiness-scoring.md.
/// </summary>
public static class HealthStatusCalculator
{
    public const int WindowDays = 28;
    public const int MinimumSamples = 5;
    private const decimal SdMultiplier = 1.5m;

    private static readonly Dictionary<HealthMetric, MetricRule> Rules = new()
    {
        // Minimum half-widths stop a very stable history from producing a range so narrow that
        // ordinary day-to-day noise reads as "out of range".
        [HealthMetric.Hrv] = new(MinHalfWidthRelative: 0.08m, MinHalfWidthAbsolute: 0, LowIsConcerning: true),
        [HealthMetric.RestingHeartRate] = new(0, 2, LowIsConcerning: false),
        [HealthMetric.SleepDuration] = new(0, 30, LowIsConcerning: true),
        [HealthMetric.SleepScore] = new(0, 5, LowIsConcerning: true),
        [HealthMetric.AvgSleepingHeartRate] = new(0, 2, LowIsConcerning: false),
        [HealthMetric.SpO2] = new(0, 1.5m, LowIsConcerning: true),
    };

    private record MetricRule(decimal MinHalfWidthRelative, decimal MinHalfWidthAbsolute, bool LowIsConcerning);

    /// <param name="history">Values of the preceding days in the window (not the day itself).</param>
    public static HealthMetricResult Evaluate(HealthMetric metric, decimal? value, IReadOnlyCollection<decimal> history)
    {
        if (history.Count < MinimumSamples)
        {
            return new(metric, value, null, null, value is null ? HealthMetricStatus.NoData : HealthMetricStatus.NoRange, false);
        }

        var rule = Rules[metric];
        var mean = history.Average();
        var variance = history.Sum(v => (v - mean) * (v - mean)) / (history.Count - 1);
        var sd = (decimal)Math.Sqrt((double)variance);
        var halfWidth = Math.Max(sd * SdMultiplier, Math.Max(rule.MinHalfWidthAbsolute, mean * rule.MinHalfWidthRelative));
        var low = Math.Round(mean - halfWidth, 1);
        var high = Math.Round(mean + halfWidth, 1);

        if (value is not { } v)
        {
            return new(metric, null, low, high, HealthMetricStatus.NoData, false);
        }

        var status = v < low ? HealthMetricStatus.BelowRange : v > high ? HealthMetricStatus.AboveRange : HealthMetricStatus.InRange;
        var concerning = (status == HealthMetricStatus.BelowRange && rule.LowIsConcerning)
            || (status == HealthMetricStatus.AboveRange && !rule.LowIsConcerning);
        return new(metric, v, low, high, status, concerning);
    }
}
