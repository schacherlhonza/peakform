namespace TrainCoach.Application.Wellness;

/// <summary>Heart rate reference values for one athlete.</summary>
/// <param name="ThresholdBpm">Lactate threshold heart rate — 60 min at it = 100 load points.</param>
public record HeartRateParameters(int RestingBpm, int MaxBpm, int ThresholdBpm, bool Female);

public record DailyLoad(DateOnly Date, decimal Load, decimal Ctl, decimal Atl, decimal? RampRate);

/// <summary>
/// Training load from heart rate, then fitness (CTL) and fatigue (ATL):
/// <list type="bullet">
/// <item><b>Activity load</b> = Banister TRIMP — Σ minutes × HRr × k·e^(b·HRr), HRr = heart rate
/// reserve fraction (k = 0.64, b = 1.92; women 0.86 / 1.67) — normalized so that one hour at the
/// threshold heart rate = 100 points. That's the scale of TSS/hrTSS, which intervals.icu's load
/// uses too, so both sources read alike. A gap between samples longer than
/// <see cref="MaxSampleGapSeconds"/> is a pause and adds nothing.</item>
/// <item><b>CTL / ATL</b> = exponentially weighted daily load over 42 / 7 days; ramp rate = CTL
/// change over the last 7 days. Days without activity are zero-load days, so both decay.</item>
/// </list>
/// </summary>
public static class TrainingLoadCalculator
{
    public const int MaxSampleGapSeconds = 30;
    public const int CtlDays = 42;
    public const int AtlDays = 7;

    public static decimal? FromStream(IReadOnlyList<int> timeOffsetsSeconds, IReadOnlyList<int?>? heartRate, HeartRateParameters p)
    {
        if (heartRate is null)
        {
            return null;
        }
        double trimp = 0;
        var any = false;
        for (var i = 0; i + 1 < timeOffsetsSeconds.Count && i < heartRate.Count; i++)
        {
            var dt = timeOffsetsSeconds[i + 1] - timeOffsetsSeconds[i];
            if (heartRate[i] is not { } bpm || dt <= 0 || dt > MaxSampleGapSeconds)
            {
                continue;
            }
            trimp += Trimp(bpm, dt / 60.0, p);
            any = true;
        }
        return any ? Normalize(trimp, p) : null;
    }

    /// <summary>Estimate for an activity without a stored stream: its average heart rate held for its duration.</summary>
    public static decimal? FromSummary(int? averageHeartRateBpm, int durationSeconds, HeartRateParameters p) =>
        averageHeartRateBpm is { } bpm && durationSeconds > 0 ? Normalize(Trimp(bpm, durationSeconds / 60.0, p), p) : null;

    public static IReadOnlyList<DailyLoad> Daily(IReadOnlyDictionary<DateOnly, decimal> loadByDate, DateOnly from, DateOnly to)
    {
        var result = new List<DailyLoad>();
        double ctl = 0, atl = 0;
        var history = new List<double>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var load = (double)loadByDate.GetValueOrDefault(date);
            ctl += (load - ctl) / CtlDays;
            atl += (load - atl) / AtlDays;
            history.Add(ctl);
            decimal? ramp = history.Count > 7 ? (decimal)Math.Round(ctl - history[^8], 1) : null;
            result.Add(new DailyLoad(date, (decimal)Math.Round(load, 1), (decimal)Math.Round(ctl, 1), (decimal)Math.Round(atl, 1), ramp));
        }
        return result;
    }

    private static double Trimp(int bpm, double minutes, HeartRateParameters p)
    {
        var reserve = Math.Clamp((bpm - p.RestingBpm) / (double)Math.Max(1, p.MaxBpm - p.RestingBpm), 0, 1);
        var (k, b) = p.Female ? (0.86, 1.67) : (0.64, 1.92);
        return minutes * reserve * k * Math.Exp(b * reserve);
    }

    private static decimal Normalize(double trimp, HeartRateParameters p)
    {
        var hourAtThreshold = Trimp(p.ThresholdBpm, 60, p);
        return hourAtThreshold > 0 ? (decimal)Math.Round(trimp / hourAtThreshold * 100, 1) : 0;
    }
}
