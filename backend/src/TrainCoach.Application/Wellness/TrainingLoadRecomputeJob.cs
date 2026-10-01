using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public interface ITrainingLoadRecomputeJob
{
    /// <summary>Recomputes PeakForm's heart-rate load for every activity of the athlete and the
    /// daily CTL/ATL series from their first activity to today. Null = every athlete with activities.</summary>
    Task RunAsync(Guid? athleteUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes <c>ActivityMetric.TrainingLoad</c> (<c>Source = PeakForm</c>) per activity and one
/// <c>TrainingLoadSnapshot</c> per day (<c>Source = PeakForm</c>) — a full rebuild each run, which
/// keeps it simple and correct when zones, profile values or old activities change (a few seconds
/// for ~3 500 activities). intervals.icu's own load/CTL stay untouched and keep precedence where
/// they exist (DailyMetricSelectionService). See docs/wellness/training-load.md.
/// </summary>
public class TrainingLoadRecomputeJob(IServiceScopeFactory scopeFactory, IDateTimeProvider clock, ILogger<TrainingLoadRecomputeJob> logger) : ITrainingLoadRecomputeJob
{
    private const int BatchSize = 200;
    private const int DefaultRestingBpm = 60;
    private const int DefaultMaxBpm = 190;

    public async Task RunAsync(Guid? athleteUserId, CancellationToken cancellationToken = default)
    {
        List<Guid> athletes;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            athletes = athleteUserId is { } id
                ? [id]
                : await db.CompletedActivities.Select(a => a.AthleteUserId).Distinct().ToListAsync(cancellationToken);
        }
        foreach (var athlete in athletes)
        {
            await RunForAthleteAsync(athlete, cancellationToken);
        }
    }

    private async Task RunForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken)
    {
        HeartRateParameters parameters;
        TimeZoneInfo zone;
        List<(Guid Id, DateTime StartedAtUtc, int? AvgHr, int Duration)> activities;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            parameters = await ResolveParametersAsync(db, athleteUserId, clock.UtcNow, cancellationToken);
            zone = AthleteLocalDates.ResolveTimeZone(await db.UserProfiles.Where(p => p.Id == athleteUserId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(cancellationToken));
            activities = (await db.CompletedActivities.AsNoTracking()
                    .Where(a => a.AthleteUserId == athleteUserId)
                    .Select(a => new { a.Id, a.StartedAtUtc, a.AverageHeartRateBpm, a.DurationSeconds })
                    .ToListAsync(cancellationToken))
                .Select(a => (a.Id, a.StartedAtUtc, a.AverageHeartRateBpm, a.DurationSeconds)).ToList();
        }
        if (activities.Count == 0)
        {
            return;
        }

        var loadByDate = new Dictionary<DateOnly, decimal>();
        foreach (var batch in activities.Chunk(BatchSize))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var ids = batch.Select(b => b.Id).ToList();
            var streams = (await db.ActivityStreams.AsNoTracking()
                    .Where(s => ids.Contains(s.ActivitySourceRecord.CompletedActivityId) && (s.Channels & ActivityStreamChannels.HeartRate) != 0)
                    .Select(s => new { s.ActivitySourceRecord.CompletedActivityId, s.Payload, s.FormatVersion, s.OriginalSampleCount, s.SampleCount })
                    .ToListAsync(cancellationToken))
                .GroupBy(s => s.CompletedActivityId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SampleCount).First());

            db.ActivityMetrics.RemoveRange(await db.ActivityMetrics
                .Where(m => ids.Contains(m.CompletedActivityId) && m.MetricType == ActivityMetricType.TrainingLoad && m.Source == DataSource.PeakForm)
                .ToListAsync(cancellationToken));

            foreach (var a in batch)
            {
                decimal? load = null;
                if (streams.TryGetValue(a.Id, out var s))
                {
                    var data = ActivityStreamCodec.Decode(s.Payload, s.FormatVersion, s.OriginalSampleCount);
                    load = TrainingLoadCalculator.FromStream(data.TimeOffsetsSeconds, data.HeartRateBpm, parameters);
                }
                load ??= TrainingLoadCalculator.FromSummary(a.AvgHr, a.Duration, parameters);
                if (load is not { } value || value <= 0)
                {
                    continue;
                }
                db.ActivityMetrics.Add(new ActivityMetric
                {
                    CompletedActivityId = a.Id,
                    MetricType = ActivityMetricType.TrainingLoad,
                    Value = value,
                    Unit = "load",
                    Source = DataSource.PeakForm,
                    RecordedAtUtc = clock.UtcNow,
                });
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(a.StartedAtUtc, zone));
                loadByDate[day] = loadByDate.GetValueOrDefault(day) + value;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(activities.Min(a => a.StartedAtUtc), zone));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, zone));
        var daily = TrainingLoadCalculator.Daily(loadByDate, first, today);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.TrainingLoadSnapshots.RemoveRange(await db.TrainingLoadSnapshots
                .Where(t => t.AthleteUserId == athleteUserId && t.Source == DataSource.PeakForm)
                .ToListAsync(cancellationToken));
            db.TrainingLoadSnapshots.AddRange(daily.Select(d => new TrainingLoadSnapshot
            {
                AthleteUserId = athleteUserId,
                Date = d.Date,
                Ctl = d.Ctl,
                Atl = d.Atl,
                RampRate = d.RampRate,
                Source = DataSource.PeakForm,
                CreatedAtUtc = clock.UtcNow,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        // Stored per-day selections were made before PeakForm's rows existed (or before they took
        // CTL/ATL precedence) — refresh them for the days another source also covers.
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var selection = scope.ServiceProvider.GetRequiredService<IDailyMetricSelectionService>();
            var overlapping = await db.TrainingLoadSnapshots
                .Where(t => t.AthleteUserId == athleteUserId && t.Source != DataSource.PeakForm)
                .Select(t => t.Date).Distinct().ToListAsync(cancellationToken);
            foreach (var date in overlapping)
            {
                await selection.RecomputeForAthleteDayAsync(athleteUserId, date, cancellationToken);
            }
        }

        logger.LogInformation(
            "Zátěž přepočítána: sportovec {Athlete}, {Activities} aktivit, {Days} dní (klid {Rest}, max {Max}, práh {Threshold} bpm).",
            athleteUserId, activities.Count, daily.Count, parameters.RestingBpm, parameters.MaxBpm, parameters.ThresholdBpm);
    }

    /// <summary>
    /// Resting: profile → median of the athlete's daily resting HR (wellness) → 60. Max: profile →
    /// highest activity max HR in the last 365 days (plausible 120–220 bpm; older history if the
    /// last year has none) → 190. Threshold: the lower bound of zone 4 of the newest zone set →
    /// 89 % of max. (Decided 2026-10-01 — see docs/wellness/training-load.md.)
    /// </summary>
    internal static async Task<HeartRateParameters> ResolveParametersAsync(IApplicationDbContext db, Guid athleteUserId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var profile = await db.AthleteProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserProfileId == athleteUserId, cancellationToken);

        var resting = profile?.RestingHeartRateBpm;
        if (resting is null)
        {
            var values = await db.RecoveryMetrics
                .Where(r => r.AthleteUserId == athleteUserId && r.RestingHeartRateBpm != null)
                .Select(r => r.RestingHeartRateBpm!.Value)
                .ToListAsync(cancellationToken);
            resting = values.Count > 0 ? values.OrderBy(v => v).ElementAt(values.Count / 2) : DefaultRestingBpm;
        }

        var max = profile?.MaxHeartRateBpm;
        if (max is null)
        {
            var plausible = db.CompletedActivities
                .Where(a => a.AthleteUserId == athleteUserId && a.MaxHeartRateBpm >= 120 && a.MaxHeartRateBpm <= 220);
            var yearAgo = nowUtc.AddDays(-365);
            max = await plausible.Where(a => a.StartedAtUtc >= yearAgo).MaxAsync(a => a.MaxHeartRateBpm, cancellationToken)
                  ?? await plausible.MaxAsync(a => a.MaxHeartRateBpm, cancellationToken)
                  ?? DefaultMaxBpm;
        }

        var zones = await db.HeartRateZones.AsNoTracking().Where(z => z.AthleteUserId == athleteUserId && z.ZoneNumber == 4)
            .OrderByDescending(z => z.EffectiveFromDate).Select(z => (int?)z.MinBpm).FirstOrDefaultAsync(cancellationToken);
        var threshold = zones is { } z4 && z4 > resting && z4 < max ? z4 : (int)Math.Round(max.Value * 0.89);

        var female = profile?.Sex is { } sex && (sex.StartsWith('F') || sex.StartsWith('f') || sex.StartsWith('Ž') || sex.StartsWith('ž'));
        return new HeartRateParameters(resting.Value, max.Value, threshold, female);
    }
}
