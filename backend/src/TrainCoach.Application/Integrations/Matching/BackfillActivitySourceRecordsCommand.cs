using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;

namespace TrainCoach.Application.Integrations.Matching;

public class BackfillActivitySourceRecordsCommand(IApplicationDbContext db, IDateTimeProvider clock) : IBackfillActivitySourceRecordsCommand
{
    public async Task<BackfillResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activities = await db.CompletedActivities
            .Include(a => a.SourceRecords)
            .ToListAsync(cancellationToken);

        var scanned = activities.Count;
        var backfilled = 0;
        var alreadyDone = 0;
        var skippedMultiple = 0;

        foreach (var activity in activities)
        {
            if (activity.PrimarySourceRecordId is not null && activity.NormalizedFingerprint is not null)
            {
                alreadyDone++;
                continue;
            }

            if (activity.SourceRecords.Count != 1)
            {
                // Zero or multiple source records pre-rework shouldn't happen (every existing
                // write path created exactly one), but if it ever does, leave it for manual
                // review rather than silently guessing which one is primary.
                skippedMultiple++;
                continue;
            }

            var record = activity.SourceRecords.Single();
            var fingerprint = ActivityFingerprint.Compute(activity.Sport, activity.StartedAtUtc, activity.DurationSeconds, activity.DistanceMeters, record.DeviceName);

            record.NormalizedFingerprint ??= fingerprint;
            activity.NormalizedFingerprint = fingerprint;
            activity.PrimarySourceRecordId = record.Id;
            activity.UpdatedAtUtc = clock.UtcNow;
            backfilled++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BackfillResult(scanned, backfilled, alreadyDone, skippedMultiple);
    }
}
