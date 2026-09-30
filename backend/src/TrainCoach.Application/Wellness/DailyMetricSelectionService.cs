using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class DailyMetricSelectionService(IApplicationDbContext db, IDateTimeProvider clock) : IDailyMetricSelectionService
{
    /// <summary>Hardcoded fallback precedence (lower wins) used when an athlete has no
    /// <see cref="AthleteMetricSourcePrecedence"/> override for a metric kind — replaces the old,
    /// never-consumed global <c>SourcePrecedence</c> enum with a real caller.</summary>
    private static readonly Dictionary<DataSource, int> DefaultSourceRank = new()
    {
        [DataSource.IntervalsIcu] = 1,
        [DataSource.Strava] = 2,
        [DataSource.Oura] = 2,
        [DataSource.Whoop] = 2,
        [DataSource.GarminDemoProvider] = 3,
        [DataSource.MySasyDemoProvider] = 3,
        [DataSource.FileImport] = 4,
        [DataSource.Manual] = 5,
    };

    public async Task RecomputeForAthleteDayAsync(Guid athleteUserId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var overrides = await db.AthleteMetricSourcePrecedences
            .Where(p => p.AthleteUserId == athleteUserId)
            .ToListAsync(cancellationToken);

        var hrv = await db.HrvMeasurements.Where(h => h.AthleteUserId == athleteUserId && h.Date == date).ToListAsync(cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.Hrv, overrides,
            hrv.Select(h => (h.Source, h.Id, (decimal?)h.RmssdMs)), cancellationToken);

        var recovery = await db.RecoveryMetrics.Where(r => r.AthleteUserId == athleteUserId && r.Date == date).ToListAsync(cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.RestingHeartRate, overrides,
            recovery.Select(r => (r.Source, r.Id, (decimal?)r.RestingHeartRateBpm)), cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.ReadinessScore, overrides,
            recovery.Select(r => (r.Source, r.Id, (decimal?)r.ReadinessScore)), cancellationToken);

        var sleep = await db.SleepRecords.Where(s => s.AthleteUserId == athleteUserId && s.Date == date).ToListAsync(cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.SleepDurationMinutes, overrides,
            sleep.Select(s => (s.Source, s.Id, (decimal?)s.DurationMinutes)), cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.SleepScore, overrides,
            sleep.Select(s => (s.Source, s.Id, (decimal?)s.SleepScore)), cancellationToken);

        var weight = await db.WeightMeasurements.Where(w => w.AthleteUserId == athleteUserId && w.Date == date).ToListAsync(cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.Weight, overrides,
            weight.Select(w => (w.Source, w.Id, (decimal?)w.WeightKg)), cancellationToken);

        var load = await db.TrainingLoadSnapshots.Where(t => t.AthleteUserId == athleteUserId && t.Date == date).ToListAsync(cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.Ctl, overrides,
            load.Select(t => (t.Source, t.Id, t.Ctl)), cancellationToken);
        await SelectAsync(athleteUserId, date, WellnessMetricKind.Atl, overrides,
            load.Select(t => (t.Source, t.Id, t.Atl)), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyMetricSelectionDto>> GetForAthleteDateRangeAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyMetricSelections
            .Where(s => s.AthleteUserId == athleteUserId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new DailyMetricSelectionDto(r.Date, r.MetricKind, r.SelectedSource, r.SelectedValue, r.PrecedenceRuleApplied)).ToList();
    }

    private async Task SelectAsync(
        Guid athleteUserId, DateOnly date, WellnessMetricKind kind,
        IReadOnlyList<AthleteMetricSourcePrecedence> overrides,
        IEnumerable<(DataSource Source, Guid Id, decimal? Value)> rows,
        CancellationToken cancellationToken)
    {
        var candidates = rows.Where(r => r.Value is not null).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var winner = candidates.OrderBy(r => RankFor(kind, r.Source, overrides)).First();
        var ruleApplied = overrides.Any(o => o.MetricKind == kind && o.Source == winner.Source)
            ? $"AthleteOverride:{winner.Source}"
            : $"DefaultPrecedence:{winner.Source}";

        var existing = await db.DailyMetricSelections.FirstOrDefaultAsync(
            s => s.AthleteUserId == athleteUserId && s.Date == date && s.MetricKind == kind, cancellationToken);
        if (existing is null)
        {
            db.DailyMetricSelections.Add(new DailyMetricSelection
            {
                AthleteUserId = athleteUserId,
                Date = date,
                MetricKind = kind,
                SelectedSource = winner.Source,
                SelectedRecordId = winner.Id,
                SelectedValue = winner.Value,
                PrecedenceRuleApplied = ruleApplied,
                ComputedAtUtc = clock.UtcNow,
            });
        }
        else
        {
            existing.SelectedSource = winner.Source;
            existing.SelectedRecordId = winner.Id;
            existing.SelectedValue = winner.Value;
            existing.PrecedenceRuleApplied = ruleApplied;
            existing.ComputedAtUtc = clock.UtcNow;
        }
    }

    private static int RankFor(WellnessMetricKind kind, DataSource source, IReadOnlyList<AthleteMetricSourcePrecedence> overrides)
    {
        var overrideRank = overrides.FirstOrDefault(o => o.MetricKind == kind && o.Source == source);
        if (overrideRank is not null)
        {
            return overrideRank.Rank;
        }

        return DefaultRankFor(source);
    }

    /// <summary>Fallback precedence for readers (e.g. <see cref="ReadinessService"/>) resolving a
    /// day that has per-source rows but no stored selection yet — e.g. manually entered data.</summary>
    internal static int DefaultRankFor(DataSource source) => DefaultSourceRank.GetValueOrDefault(source, int.MaxValue);
}
