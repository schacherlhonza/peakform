using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

/// <summary>Replaces one activity's best efforts — shared by the archive import, the stream
/// backfill (both precise, from the full file) and the background recompute (stored stream).</summary>
public static class BestEffortStore
{
    /// <summary>Stages the replacement on <paramref name="db"/>; the caller saves.</summary>
    public static async Task ReplaceAsync(
        IApplicationDbContext db, CompletedActivity activity, IReadOnlyList<BestEffortResult> results, bool precise,
        IEnumerable<ActivityStream> streams, DateTime nowUtc, CancellationToken cancellationToken)
    {
        db.ActivityBestEfforts.RemoveRange(await db.ActivityBestEfforts
            .Where(e => e.CompletedActivityId == activity.Id)
            .ToListAsync(cancellationToken));
        foreach (var r in results)
        {
            db.ActivityBestEfforts.Add(new ActivityBestEffort
            {
                CompletedActivityId = activity.Id,
                AthleteUserId = activity.AthleteUserId,
                Sport = activity.Sport,
                Type = r.Type,
                Value = r.Value,
                StartOffsetSeconds = r.StartOffsetSeconds,
                ActivityStartedAtUtc = activity.StartedAtUtc,
                IsPrecise = precise,
                CreatedAtUtc = nowUtc,
            });
        }
        foreach (var stream in streams)
        {
            stream.BestEffortsVersion = BestEffortCalculator.Version;
            stream.BestEffortsPrecise = precise;
        }
    }
}

public interface IBestEffortRecomputeJob
{
    /// <summary>Computes best efforts from stored streams that don't have them at the current
    /// <see cref="BestEffortCalculator.Version"/> — for one athlete, or everyone (null).</summary>
    Task RunAsync(Guid? athleteUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Background pass over stored streams lacking best efforts — the history imported before efforts
/// existed, and anything left behind by an algorithm version bump. Uses the downsampled stream
/// (±2–5 s on distance efforts, marked <c>IsPrecise = false</c>); a later archive import or
/// stream download replaces them with precise ones. Runs once at startup and after imports.
/// </summary>
public class BestEffortRecomputeJob(IServiceScopeFactory scopeFactory, IDateTimeProvider clock, ILogger<BestEffortRecomputeJob> logger) : IBestEffortRecomputeJob
{
    private const int BatchSize = 100;

    public async Task RunAsync(Guid? athleteUserId, CancellationToken cancellationToken = default)
    {
        var processed = 0;
        while (true)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            // Streams are the unit of work; an activity with two (archive + intervals.icu) is done
            // from the first, and both are marked.
            var activityIds = await db.ActivityStreams
                .Where(s => s.BestEffortsVersion < BestEffortCalculator.Version
                    && (athleteUserId == null || s.ActivitySourceRecord.CompletedActivity.AthleteUserId == athleteUserId))
                .Select(s => s.ActivitySourceRecord.CompletedActivityId)
                .Distinct()
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (activityIds.Count == 0)
            {
                break;
            }

            var activities = await db.CompletedActivities
                .Include(a => a.SourceRecords).ThenInclude(sr => sr.Stream)
                .Where(a => activityIds.Contains(a.Id))
                .ToListAsync(cancellationToken);
            foreach (var activity in activities)
            {
                var streams = activity.SourceRecords.Where(sr => sr.Stream is not null).Select(sr => sr.Stream!).ToList();
                // A precise result already stored from the full file stays; only re-mark the version.
                if (streams.Any(s => s.BestEffortsPrecise))
                {
                    streams.ForEach(s => s.BestEffortsVersion = BestEffortCalculator.Version);
                    continue;
                }
                var stream = streams.OrderByDescending(s => s.SampleCount).First();
                var data = ActivityStreamCodec.Decode(stream.Payload, stream.FormatVersion, stream.OriginalSampleCount);
                await BestEffortStore.ReplaceAsync(db, activity, BestEffortCalculator.Compute(activity.Sport, data), precise: false, streams, clock.UtcNow, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            processed += activityIds.Count;
        }

        if (processed > 0)
        {
            logger.LogInformation("Nejlepší výkony spočítány u {Count} aktivit.", processed);
        }
    }
}
