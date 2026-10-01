using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Integrations;

/// <summary>What disconnecting Strava removes — shown to the athlete before they confirm.</summary>
/// <param name="ActivitiesDeleted">Activities known only through the Strava API — deleted entirely.</param>
/// <param name="SourcesRemoved">Merged activities that keep another source (intervals.icu, archive) and only lose the Strava API one.</param>
/// <param name="KeptFromArchive">Strava API records whose activity also came in the athlete's own data archive — kept, as archive data.</param>
public record StravaDisconnectImpactDto(int ActivitiesDeleted, int SourcesRemoved, int KeptFromArchive);

public interface IStravaApiDataPurgeService
{
    Task<StravaDisconnectImpactDto> GetImpactAsync(Guid athleteUserId, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes the athlete's data obtained through the Strava API. Stages and saves.</summary>
    Task<StravaDisconnectImpactDto> PurgeAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Strava API Agreement: once access ends, all data obtained through the API must be deleted
/// permanently (docs/integrations-research.md §1). "Obtained through the API" = source records
/// with <c>Source = Strava</c> that didn't come from a data-archive import. The archive is the
/// athlete's own export and stays — including API records the archive import attached a stream
/// to: those activities are in the archive too, so they're re-labelled as archive data (minus the
/// raw API payload) instead of deleted. Deletion is hard, soft-deleted activities included.
/// </summary>
public class StravaApiDataPurgeService(IApplicationDbContext db) : IStravaApiDataPurgeService
{
    private sealed record Plan(
        List<ActivitySourceRecord> ToRemove,
        List<ActivitySourceRecord> ToRetag,
        Guid? RetagImportId,
        List<CompletedActivity> ActivitiesToDelete,
        List<CompletedActivity> ActivitiesToRepoint);

    public async Task<StravaDisconnectImpactDto> GetImpactAsync(Guid athleteUserId, CancellationToken cancellationToken = default) =>
        ToImpact(await BuildPlanAsync(athleteUserId, cancellationToken));

    public async Task<StravaDisconnectImpactDto> PurgeAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        var plan = await BuildPlanAsync(athleteUserId, cancellationToken);

        foreach (var record in plan.ToRetag)
        {
            record.StravaArchiveImportId = plan.RetagImportId;
            record.RawPayloadJson = null;
            record.RawPayloadRetained = false;
        }

        var removedIds = plan.ToRemove.Select(r => r.Id).ToHashSet();
        var deletedActivityIds = plan.ActivitiesToDelete.Select(a => a.Id).ToList();

        // Audit rows that would point at data that no longer exists.
        db.MergeDecisions.RemoveRange(await db.MergeDecisions
            .Where(m => removedIds.Contains(m.AbsorbedSourceRecordId) || deletedActivityIds.Contains(m.SurvivingActivityId))
            .ToListAsync(cancellationToken));
        db.DuplicateCandidates.RemoveRange(await db.DuplicateCandidates
            .Where(c => deletedActivityIds.Contains(c.ActivityAId) || deletedActivityIds.Contains(c.ActivityBId))
            .ToListAsync(cancellationToken));
        // The athlete's own feedback survives, just no longer linked to the deleted activity.
        foreach (var feedback in await db.TrainingFeedbacks
                     .Where(f => f.CompletedActivityId != null && deletedActivityIds.Contains(f.CompletedActivityId.Value))
                     .ToListAsync(cancellationToken))
        {
            feedback.CompletedActivityId = null;
        }

        // Cascades to its source records, metrics and streams.
        db.CompletedActivities.RemoveRange(plan.ActivitiesToDelete);

        foreach (var activity in plan.ActivitiesToRepoint)
        {
            var remaining = activity.SourceRecords.Where(sr => !removedIds.Contains(sr.Id)).ToList();
            db.ActivitySourceRecords.RemoveRange(activity.SourceRecords.Where(sr => removedIds.Contains(sr.Id)));
            if (activity.PrimarySourceRecordId is { } primary && removedIds.Contains(primary))
            {
                // The displayed values stay (another source reported the same activity; the
                // matcher only merged near-identical records), but they're now owned by it.
                var next = remaining.FirstOrDefault(sr => sr.Source != DataSource.Strava) ?? remaining[0];
                activity.PrimarySourceRecordId = next.Id;
                activity.NormalizedFingerprint = next.NormalizedFingerprint;
            }
            if (remaining.Count == 1 && activity.MatchStatus == ActivityMatchStatus.AutoMerged)
            {
                activity.MatchStatus = ActivityMatchStatus.Unambiguous;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToImpact(plan);
    }

    private async Task<Plan> BuildPlanAsync(Guid athleteUserId, CancellationToken cancellationToken)
    {
        var apiRecords = await db.ActivitySourceRecords.IgnoreQueryFilters()
            .Include(sr => sr.Stream)
            .Where(sr => sr.Source == DataSource.Strava && sr.StravaArchiveImportId == null
                && sr.CompletedActivity.AthleteUserId == athleteUserId)
            .ToListAsync(cancellationToken);

        var latestArchiveImportId = await db.StravaArchiveImports
            .Where(i => i.AthleteUserId == athleteUserId && i.Status == StravaArchiveImportStatus.Succeeded)
            .OrderByDescending(i => i.FinishedAtUtc)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var toRetag = latestArchiveImportId is null
            ? []
            : apiRecords.Where(sr => sr.Stream?.Origin == ActivityStreamOrigin.StravaArchive).ToList();
        var toRemove = apiRecords.Except(toRetag).ToList();
        var removedIds = toRemove.Select(r => r.Id).ToHashSet();

        var activityIds = toRemove.Select(r => r.CompletedActivityId).Distinct().ToList();
        var activities = await db.CompletedActivities.IgnoreQueryFilters()
            .Include(a => a.SourceRecords)
            .Where(a => activityIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var toDelete = activities.Where(a => a.SourceRecords.All(sr => removedIds.Contains(sr.Id))).ToList();
        var toRepoint = activities.Except(toDelete).ToList();
        return new Plan(toRemove, toRetag, latestArchiveImportId, toDelete, toRepoint);
    }

    private static StravaDisconnectImpactDto ToImpact(Plan plan) =>
        new(plan.ActivitiesToDelete.Count, plan.ActivitiesToRepoint.Count, plan.ToRetag.Count);
}
