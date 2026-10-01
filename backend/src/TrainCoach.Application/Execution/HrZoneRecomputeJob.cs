using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

public interface IHrZoneRecomputeJob
{
    /// <summary>(Re)computes time in heart rate zones for the athlete's activities with a stored
    /// heart rate stream. <paramref name="onlyMissing"/>: skip activities that already have it
    /// (after new streams arrived); false: recompute everything (the zones changed).</summary>
    Task RunAsync(Guid athleteUserId, bool onlyMissing, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes <c>TimeInHrZone1..7</c> metrics (<c>Source = PeakForm</c>) from the activity's stored
/// stream and the zone set in effect on its date. Activities older than the athlete's first zone
/// set use that earliest set — the closest estimate there is (docs/wellness/hr-zones.md).
/// Queued after zones are saved, after an archive import, and after a stream backfill.
/// </summary>
public class HrZoneRecomputeJob(IServiceScopeFactory scopeFactory, IDateTimeProvider clock, ILogger<HrZoneRecomputeJob> logger) : IHrZoneRecomputeJob
{
    private const int BatchSize = 200;

    internal static readonly ActivityMetricType[] ZoneMetricTypes =
    [
        ActivityMetricType.TimeInHrZone1, ActivityMetricType.TimeInHrZone2, ActivityMetricType.TimeInHrZone3,
        ActivityMetricType.TimeInHrZone4, ActivityMetricType.TimeInHrZone5, ActivityMetricType.TimeInHrZone6,
        ActivityMetricType.TimeInHrZone7, ActivityMetricType.TimeBelowHrZones,
    ];

    public async Task RunAsync(Guid athleteUserId, bool onlyMissing, CancellationToken cancellationToken = default)
    {
        List<(DateOnly From, List<HrZoneBounds> Zones)> zoneSets;
        List<(Guid ActivityId, DateTime StartedAtUtc)> activities;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            zoneSets = (await db.HeartRateZones.AsNoTracking().Where(z => z.AthleteUserId == athleteUserId).ToListAsync(cancellationToken))
                .GroupBy(z => z.EffectiveFromDate)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Select(z => new HrZoneBounds(z.ZoneNumber, z.MinBpm, z.MaxBpm)).ToList()))
                .ToList();

            var query = db.CompletedActivities.AsNoTracking()
                .Where(a => a.AthleteUserId == athleteUserId
                    && a.SourceRecords.Any(sr => sr.Stream != null && (sr.Stream.Channels & ActivityStreamChannels.HeartRate) != 0));
            if (onlyMissing)
            {
                query = query.Where(a => !a.AdditionalMetrics.Any(m => ZoneMetricTypes.Contains(m.MetricType)));
            }
            activities = (await query.Select(a => new { a.Id, a.StartedAtUtc }).ToListAsync(cancellationToken))
                .Select(a => (a.Id, a.StartedAtUtc)).ToList();
        }

        if (zoneSets.Count == 0)
        {
            if (!onlyMissing)
            {
                await ClearAsync(athleteUserId, cancellationToken);
            }
            return;
        }

        foreach (var batch in activities.Chunk(BatchSize))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var ids = batch.Select(b => b.ActivityId).ToList();

            var streams = await db.ActivityStreams.AsNoTracking()
                .Where(s => ids.Contains(s.ActivitySourceRecord.CompletedActivityId) && (s.Channels & ActivityStreamChannels.HeartRate) != 0)
                .Select(s => new { s.ActivitySourceRecord.CompletedActivityId, s.Payload, s.FormatVersion, s.OriginalSampleCount })
                .ToListAsync(cancellationToken);
            db.ActivityMetrics.RemoveRange(await db.ActivityMetrics
                .Where(m => ids.Contains(m.CompletedActivityId) && ZoneMetricTypes.Contains(m.MetricType))
                .ToListAsync(cancellationToken));

            foreach (var (activityId, startedAt) in batch)
            {
                var stream = streams.FirstOrDefault(s => s.CompletedActivityId == activityId);
                if (stream is null)
                {
                    continue;
                }
                var data = ActivityStreamCodec.Decode(stream.Payload, stream.FormatVersion, stream.OriginalSampleCount);
                var zones = ZonesOn(zoneSets, DateOnly.FromDateTime(startedAt));
                foreach (var (zoneNumber, seconds) in HrZoneTimeCalculator.Compute(data.TimeOffsetsSeconds, data.HeartRateBpm, zones, data.SpeedMetersPerSecond))
                {
                    if (zoneNumber is < 0 or > 7 || seconds <= 0)
                    {
                        continue;
                    }
                    db.ActivityMetrics.Add(new ActivityMetric
                    {
                        CompletedActivityId = activityId,
                        MetricType = zoneNumber == HrZoneTimeCalculator.BelowZones ? ActivityMetricType.TimeBelowHrZones : ZoneMetricTypes[zoneNumber - 1],
                        Value = seconds,
                        Unit = "s",
                        Source = DataSource.PeakForm,
                        RecordedAtUtc = clock.UtcNow,
                    });
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Čas v tepových zónách přepočítán u {Count} aktivit sportovce {Athlete}.", activities.Count, athleteUserId);
    }

    /// <summary>The set in effect on <paramref name="date"/>; before the first set, the first set.</summary>
    internal static List<HrZoneBounds> ZonesOn(List<(DateOnly From, List<HrZoneBounds> Zones)> zoneSets, DateOnly date) =>
        zoneSets.LastOrDefault(s => s.From <= date).Zones ?? zoneSets[0].Zones;

    private async Task ClearAsync(Guid athleteUserId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        db.ActivityMetrics.RemoveRange(await db.ActivityMetrics
            .Where(m => m.CompletedActivity.AthleteUserId == athleteUserId && ZoneMetricTypes.Contains(m.MetricType))
            .ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }
}
