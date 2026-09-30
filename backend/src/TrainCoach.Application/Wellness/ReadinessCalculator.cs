using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public enum ReadinessFactor
{
    Hrv = 1,
    RestingHeartRate = 2,
    Sleep = 3,
    TrainingLoad = 4,
    Subjective = 5,
}

/// <summary>Vendor = a provider's own readiness score (Oura/WHOOP/…) shown as-is; Computed =
/// PeakForm's transparent estimate from the individual signals below.</summary>
public enum ReadinessScoreSource
{
    Vendor = 1,
    Computed = 2,
}

/// <summary>Everything known about one athlete-day that readiness is derived from. Baselines are
/// means over the preceding days (never including the day itself).</summary>
public record ReadinessInputs(
    decimal? HrvRmssdMs,
    decimal? HrvBaselineMs,
    int? RestingHeartRateBpm,
    decimal? RestingHeartRateBaselineBpm,
    int? SleepDurationMinutes,
    int? SleepScore,
    decimal? Ctl,
    decimal? Atl,
    IReadOnlyList<WellnessScale> SubjectiveRatings,
    decimal? VendorReadinessScore);

public record ReadinessComponent(ReadinessFactor Factor, int SubScore, int Weight);

public record ReadinessResult(int? Score, ReadinessScoreSource? Source, IReadOnlyList<ReadinessComponent> Components);

/// <summary>
/// PeakForm's own readiness estimate, for sources that (like Garmin via intervals.icu) deliver
/// HRV/resting HR/sleep/load but no readiness score of their own. Deliberately simple and
/// explainable — each signal maps to a 0-100 sub-score and the result is their weighted mean over
/// whatever is available; the UI shows every sub-score, so nothing is a black box.
/// </summary>
public static class ReadinessCalculator
{
    private const int HrvWeight = 30;
    private const int SleepWeight = 25;
    private const int RestingHeartRateWeight = 20;
    private const int TrainingLoadWeight = 15;
    private const int SubjectiveWeight = 10;

    /// <summary>Fewer signals than this is too thin to call a score — the card shows the raw
    /// metrics instead of a number that would mostly reflect a single reading.</summary>
    private const int MinimumComponents = 2;

    public static ReadinessResult Calculate(ReadinessInputs inputs)
    {
        var components = new List<ReadinessComponent>();

        // HRV relative to personal baseline: at baseline ≈ 80, −10 % ≈ 50, −20 % ≈ 20.
        if (inputs.HrvRmssdMs is { } hrv && inputs.HrvBaselineMs is { } hrvBaseline && hrvBaseline > 0)
        {
            var ratio = hrv / hrvBaseline;
            components.Add(new(ReadinessFactor.Hrv, Clamp(80 + (ratio - 1) * 300), HrvWeight));
        }

        // Resting HR above baseline signals incomplete recovery / illness: +1 bpm ≈ −8 points.
        if (inputs.RestingHeartRateBpm is { } rhr && inputs.RestingHeartRateBaselineBpm is { } rhrBaseline)
        {
            components.Add(new(ReadinessFactor.RestingHeartRate, Clamp(80 - (rhr - rhrBaseline) * 8), RestingHeartRateWeight));
        }

        // The provider's sleep score already weighs duration, stages and interruptions; duration
        // alone is only the fallback (8 h ≈ 90, 7 h ≈ 72, 6 h ≈ 54).
        if (inputs.SleepScore is { } sleepScore)
        {
            components.Add(new(ReadinessFactor.Sleep, Clamp(sleepScore), SleepWeight));
        }
        else if (inputs.SleepDurationMinutes is { } sleepMinutes)
        {
            components.Add(new(ReadinessFactor.Sleep, Clamp(90 - (480 - sleepMinutes) * 0.3m), SleepWeight));
        }

        // Form (TSB = CTL − ATL): fresh ≥ 0 scores high, −10…−30 is the productive-training band,
        // below −30 is accumulated fatigue.
        if (inputs.Ctl is { } ctl && inputs.Atl is { } atl)
        {
            components.Add(new(ReadinessFactor.TrainingLoad, Clamp(80 + (ctl - atl) * 1.5m), TrainingLoadWeight));
        }

        // Morning check-in, every dimension on the same "higher = better" 1-5 scale.
        if (inputs.SubjectiveRatings.Count > 0)
        {
            var average = (decimal)inputs.SubjectiveRatings.Average(r => (int)r);
            components.Add(new(ReadinessFactor.Subjective, Clamp((average - 1) / 4 * 100), SubjectiveWeight));
        }

        if (inputs.VendorReadinessScore is { } vendor)
        {
            return new(Clamp(vendor), ReadinessScoreSource.Vendor, components);
        }

        if (components.Count < MinimumComponents)
        {
            return new(null, null, components);
        }

        var weighted = components.Sum(c => (decimal)c.SubScore * c.Weight) / components.Sum(c => c.Weight);
        return new(Clamp(weighted), ReadinessScoreSource.Computed, components);
    }

    private static int Clamp(decimal value) => (int)Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero);
}
