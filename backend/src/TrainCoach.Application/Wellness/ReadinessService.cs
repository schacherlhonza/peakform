using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record ReadinessComponentDto(ReadinessFactor Factor, int SubScore, int Weight);

public record HealthMetricDto(HealthMetric Metric, decimal? Value, decimal? RangeLow, decimal? RangeHigh, HealthMetricStatus Status, bool IsConcerning);

/// <summary>Readiness for the most recent day (within a short lookback) that has any recovery
/// data, plus the raw signals and per-factor sub-scores it was derived from.</summary>
public record ReadinessDto(
    DateOnly? Date,
    bool IsToday,
    int? Score,
    ReadinessScoreSource? ScoreSource,
    decimal? HrvRmssdMs,
    decimal? HrvBaselineMs,
    int? RestingHeartRateBpm,
    decimal? RestingHeartRateBaselineBpm,
    int? SleepDurationMinutes,
    int? SleepScore,
    decimal? Ctl,
    decimal? Atl,
    bool? HasPainOrIllness,
    IReadOnlyList<ReadinessComponentDto> Components,
    IReadOnlyList<HealthMetricDto> HealthStatus);

public interface IReadinessService
{
    Task<ReadinessDto> GetForAthleteAsync(Guid athleteUserId, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves one value per metric per day (the stored <c>DailyMetricSelection</c> when present,
/// otherwise the default source precedence over the raw per-source rows) and feeds
/// <see cref="ReadinessCalculator"/>. A source's own readiness score wins when it has one.
/// </summary>
public class ReadinessService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard) : IReadinessService
{
    /// <summary>"Today has no sync yet" falls back to the latest reading this many days back.</summary>
    private const int LookbackDays = 3;

    private const int BaselineDays = 7;
    private const int MinimumBaselineSamples = 3;

    public async Task<ReadinessDto> GetForAthleteAsync(Guid athleteUserId, DateOnly date, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        // Health-status ranges need the longest history; the readiness baseline uses a subset.
        var from = date.AddDays(-(LookbackDays + HealthStatusCalculator.WindowDays));

        var hrvRows = await db.HrvMeasurements
            .Where(h => h.AthleteUserId == athleteUserId && h.Date >= from && h.Date <= date)
            .Select(h => new SourcedValue(h.Date, h.Source, h.RmssdMs))
            .ToListAsync(cancellationToken);
        var recoveryRows = await db.RecoveryMetrics
            .Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= date)
            .ToListAsync(cancellationToken);
        var sleepRows = await db.SleepRecords
            .Where(s => s.AthleteUserId == athleteUserId && s.Date >= from && s.Date <= date)
            .ToListAsync(cancellationToken);
        var loadRows = await db.TrainingLoadSnapshots
            .Where(t => t.AthleteUserId == athleteUserId && t.Date >= from && t.Date <= date)
            .ToListAsync(cancellationToken);
        var selections = await db.DailyMetricSelections
            .Where(s => s.AthleteUserId == athleteUserId && s.Date >= from && s.Date <= date)
            .ToListAsync(cancellationToken);

        var hrv = Resolve(WellnessMetricKind.Hrv, hrvRows, selections);
        var restingHr = Resolve(WellnessMetricKind.RestingHeartRate,
            recoveryRows.Select(r => new SourcedValue(r.Date, r.Source, r.RestingHeartRateBpm)), selections);
        var vendorReadiness = Resolve(WellnessMetricKind.ReadinessScore,
            recoveryRows.Select(r => new SourcedValue(r.Date, r.Source, r.ReadinessScore)), selections);
        var sleepMinutes = Resolve(WellnessMetricKind.SleepDurationMinutes,
            sleepRows.Select(s => new SourcedValue(s.Date, s.Source, s.DurationMinutes)), selections);
        var sleepScore = Resolve(WellnessMetricKind.SleepScore,
            sleepRows.Select(s => new SourcedValue(s.Date, s.Source, s.SleepScore)), selections);
        var ctl = Resolve(WellnessMetricKind.Ctl, loadRows.Select(t => new SourcedValue(t.Date, t.Source, t.Ctl)), selections);
        var atl = Resolve(WellnessMetricKind.Atl, loadRows.Select(t => new SourcedValue(t.Date, t.Source, t.Atl)), selections);
        var sleepingHr = Resolve(null, sleepRows.Select(s => new SourcedValue(s.Date, s.Source, s.AvgSleepingHeartRateBpm)), selections);
        var spO2 = Resolve(null, recoveryRows.Select(r => new SourcedValue(r.Date, r.Source, r.SpO2Percent)), selections);

        // The day readiness is about: the latest one in the lookback with a recovery signal
        // (training load alone doesn't count — it is derived from workouts, not from the body).
        DateOnly? dataDate = null;
        for (var d = date; d >= date.AddDays(-LookbackDays); d = d.AddDays(-1))
        {
            if (hrv.ContainsKey(d) || restingHr.ContainsKey(d) || sleepMinutes.ContainsKey(d) || sleepScore.ContainsKey(d) || vendorReadiness.ContainsKey(d))
            {
                dataDate = d;
                break;
            }
        }

        if (dataDate is not { } day)
        {
            return new ReadinessDto(null, false, null, null, null, null, null, null, null, null, null, null, null, [], []);
        }

        var checkIn = await db.DailyCheckIns
            .Where(c => c.AthleteUserId == athleteUserId && c.Date == day && c.Type == CheckInType.Morning)
            .FirstOrDefaultAsync(cancellationToken);
        var subjective = checkIn is null
            ? []
            : new[] { checkIn.Energy, checkIn.Fatigue, checkIn.LegsFeeling, checkIn.Stress, checkIn.SleepQuality, checkIn.MuscleSoreness, checkIn.Motivation }
                .Where(v => v is not null).Select(v => v!.Value).ToList();

        var inputs = new ReadinessInputs(
            HrvRmssdMs: ValueOn(hrv, day),
            HrvBaselineMs: Baseline(hrv, day),
            RestingHeartRateBpm: ToInt(ValueOn(restingHr, day)),
            RestingHeartRateBaselineBpm: Baseline(restingHr, day),
            SleepDurationMinutes: ToInt(ValueOn(sleepMinutes, day)),
            SleepScore: ToInt(ValueOn(sleepScore, day)),
            Ctl: ValueOn(ctl, day),
            Atl: ValueOn(atl, day),
            SubjectiveRatings: subjective,
            VendorReadinessScore: ValueOn(vendorReadiness, day));

        var result = ReadinessCalculator.Calculate(inputs);

        return new ReadinessDto(
            day, day == date, result.Score, result.Source,
            inputs.HrvRmssdMs, Round(inputs.HrvBaselineMs), inputs.RestingHeartRateBpm, Round(inputs.RestingHeartRateBaselineBpm),
            inputs.SleepDurationMinutes, inputs.SleepScore, Round(inputs.Ctl), Round(inputs.Atl),
            checkIn?.HasPainOrIllness,
            result.Components.Select(c => new ReadinessComponentDto(c.Factor, c.SubScore, c.Weight)).ToList(),
            new[]
            {
                Health(HealthMetric.Hrv, hrv, day),
                Health(HealthMetric.RestingHeartRate, restingHr, day),
                Health(HealthMetric.SleepDuration, sleepMinutes, day),
                Health(HealthMetric.SleepScore, sleepScore, day),
                Health(HealthMetric.AvgSleepingHeartRate, sleepingHr, day),
                Health(HealthMetric.SpO2, spO2, day),
            }.ToList());
    }

    private static HealthMetricDto Health(HealthMetric metric, Dictionary<DateOnly, decimal> values, DateOnly day)
    {
        var history = values.Where(kv => kv.Key < day && kv.Key >= day.AddDays(-HealthStatusCalculator.WindowDays)).Select(kv => kv.Value).ToList();
        var r = HealthStatusCalculator.Evaluate(metric, ValueOn(values, day), history);
        return new HealthMetricDto(r.Metric, r.Value, r.RangeLow, r.RangeHigh, r.Status, r.IsConcerning);
    }

    private record SourcedValue(DateOnly Date, DataSource Source, decimal? Value);

    private static Dictionary<DateOnly, decimal> Resolve(
        WellnessMetricKind? kind, IEnumerable<SourcedValue> rows, IReadOnlyList<Domain.Wellness.DailyMetricSelection> selections)
    {
        var result = rows
            .Where(r => r.Value is not null)
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => DailyMetricSelectionService.DefaultRankFor(r.Source)).First().Value!.Value);

        // A stored selection reflects the athlete's per-metric source overrides — it wins.
        foreach (var selection in selections.Where(s => s.MetricKind == kind && s.SelectedValue is not null))
        {
            result[selection.Date] = selection.SelectedValue!.Value;
        }

        return result;
    }

    /// <summary>Not <c>GetValueOrDefault</c> — that yields 0 for a missing day, which would read
    /// as a real (terrible) reading instead of "no data".</summary>
    private static decimal? ValueOn(Dictionary<DateOnly, decimal> values, DateOnly day) =>
        values.TryGetValue(day, out var value) ? value : null;

    private static decimal? Baseline(Dictionary<DateOnly, decimal> values, DateOnly day)
    {
        var window = values.Where(kv => kv.Key < day && kv.Key >= day.AddDays(-BaselineDays)).Select(kv => kv.Value).ToList();
        return window.Count >= MinimumBaselineSamples ? window.Average() : null;
    }

    private static int? ToInt(decimal? value) => value is { } v ? (int)Math.Round(v) : null;

    private static decimal? Round(decimal? value) => value is { } v ? Math.Round(v, 1) : null;
}
