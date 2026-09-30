using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Integrations;

public enum IngestionOutcome
{
    Created,
    Merged,
    FlaggedForReview,
    SkippedDuplicate,
}

/// <summary>Which run/import produced a source record — exactly one of these is set.</summary>
public record ActivitySourceOrigin(Guid? SynchronizationRunId = null, Guid? StravaArchiveImportId = null);

/// <summary>
/// One incoming activity through the level-1 (exact external id) check, then — on a miss — the
/// matcher, then the connector policy for what to do with the outcome. Shared by the live sync
/// (<see cref="SyncOrchestrator"/>) and the Strava archive import, so both dedup identically. See
/// docs/integrations/canonical-data-and-deduplication-plan.md for the full pipeline.
/// Only stages changes on the context — callers own <c>SaveChangesAsync</c>.
/// </summary>
public interface IActivityIngestionService
{
    Task<IngestionOutcome> IngestAsync(
        Guid athleteUserId, ExternalActivity external, DataSource dataSource, ConnectorMode policyMode,
        ActivitySourceOrigin origin, CancellationToken cancellationToken = default);

    /// <summary>Dry run of <see cref="IngestAsync"/>: the same decision, nothing written.</summary>
    Task<IngestionOutcome> PreviewAsync(
        Guid athleteUserId, ExternalActivity external, DataSource dataSource, ConnectorMode policyMode,
        CancellationToken cancellationToken = default);
}

public class ActivityIngestionService(
    IApplicationDbContext db,
    IActivityMatchingService matchingService,
    IDateTimeProvider clock) : IActivityIngestionService
{
    public async Task<IngestionOutcome> IngestAsync(
        Guid athleteUserId, ExternalActivity external, DataSource dataSource, ConnectorMode policyMode,
        ActivitySourceOrigin origin, CancellationToken cancellationToken = default)
    {
        // Level 1 — idempotent upsert. The one check that must never be replaced by fuzzy/fingerprint
        // matching.
        if (await ExistsAsync(external, dataSource, cancellationToken))
        {
            return IngestionOutcome.SkippedDuplicate;
        }

        var newRecord = BuildSourceRecord(external, dataSource, origin);

        // Levels 2-5 — only consulted after the level-1 exact-match check misses.
        var match = await matchingService.FindOrScoreMatchAsync(athleteUserId, external, dataSource, cancellationToken);

        switch (match.Outcome)
        {
            case ActivityMatchOutcome.AutoMerge:
                var existingActivity = await db.CompletedActivities.Include(a => a.SourceRecords)
                    .FirstAsync(a => a.Id == match.MatchedActivityId, cancellationToken);
                AttachAsMergedSource(existingActivity, newRecord, external, policyMode, match);
                return IngestionOutcome.Merged;

            case ActivityMatchOutcome.FlagForReview:
                // Never silently withhold data — still create the activity, just flagged, and
                // queue it for human review instead of deciding automatically.
                if (policyMode == ConnectorMode.EnrichmentOnly)
                {
                    // An enrichment-only source never creates a new canonical activity on its own;
                    // a review-band candidate with no committed create is simply dropped (logged
                    // via the skip counter) rather than left half-attached.
                    return IngestionOutcome.SkippedDuplicate;
                }

                var created = CreateNewActivity(athleteUserId, external, newRecord, dataSource);
                created.MatchStatus = ActivityMatchStatus.PendingReview;
                db.DuplicateCandidates.Add(new DuplicateCandidate
                {
                    AthleteUserId = athleteUserId,
                    ActivityAId = match.MatchedActivityId!.Value,
                    ActivityBId = created.Id,
                    ConfidenceScore = match.ConfidenceScore ?? 0,
                    ScoringBreakdownJson = match.ScoringBreakdownJson ?? "{}",
                    Status = DuplicateCandidateStatus.Pending,
                    CreatedAtUtc = clock.UtcNow,
                });
                return IngestionOutcome.FlaggedForReview;

            default:
                if (policyMode == ConnectorMode.EnrichmentOnly)
                {
                    // Enrichment-only sources are defined as "only decorates an activity another
                    // source already reported" — with no candidate to decorate, drop the item.
                    return IngestionOutcome.SkippedDuplicate;
                }
                CreateNewActivity(athleteUserId, external, newRecord, dataSource);
                return IngestionOutcome.Created;
        }
    }

    public async Task<IngestionOutcome> PreviewAsync(
        Guid athleteUserId, ExternalActivity external, DataSource dataSource, ConnectorMode policyMode,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(external, dataSource, cancellationToken))
        {
            return IngestionOutcome.SkippedDuplicate;
        }

        var match = await matchingService.FindOrScoreMatchAsync(athleteUserId, external, dataSource, cancellationToken);
        return match.Outcome switch
        {
            ActivityMatchOutcome.AutoMerge => IngestionOutcome.Merged,
            _ when policyMode == ConnectorMode.EnrichmentOnly => IngestionOutcome.SkippedDuplicate,
            ActivityMatchOutcome.FlagForReview => IngestionOutcome.FlaggedForReview,
            _ => IngestionOutcome.Created,
        };
    }

    private Task<bool> ExistsAsync(ExternalActivity external, DataSource dataSource, CancellationToken cancellationToken) =>
        db.ActivitySourceRecords.AnyAsync(p => p.Source == dataSource && p.ExternalId == external.ExternalId, cancellationToken);

    private ActivitySourceRecord BuildSourceRecord(ExternalActivity external, DataSource dataSource, ActivitySourceOrigin origin) => new()
    {
        Source = dataSource,
        ExternalId = external.ExternalId,
        SynchronizationRunId = origin.SynchronizationRunId,
        StravaArchiveImportId = origin.StravaArchiveImportId,
        FetchedAtUtc = clock.UtcNow,
        RawPayloadRetained = external.RawPayloadJson is not null,
        RawPayloadJson = external.RawPayloadJson,
        DeviceName = external.DeviceName,
        FitFileUuid = external.FitFileUuid,
        NormalizedFingerprint = ActivityFingerprint.Compute(external.Sport, external.StartedAtUtc, external.DurationSeconds, external.DistanceMeters, external.DeviceName),
    };

    private CompletedActivity CreateNewActivity(Guid athleteUserId, ExternalActivity external, ActivitySourceRecord sourceRecord, DataSource dataSource)
    {
        var activity = new CompletedActivity
        {
            AthleteUserId = athleteUserId,
            Sport = external.Sport,
            Title = external.Title,
            StartedAtUtc = external.StartedAtUtc,
            DurationSeconds = external.DurationSeconds,
            DistanceMeters = external.DistanceMeters,
            ElevationGainMeters = external.ElevationGainMeters,
            AverageHeartRateBpm = external.AverageHeartRateBpm,
            MaxHeartRateBpm = external.MaxHeartRateBpm,
            AveragePaceSecondsPerKm = external.AveragePaceSecondsPerKm,
            AveragePowerWatts = external.AveragePowerWatts,
            Calories = external.Calories,
            CreatedAtUtc = clock.UtcNow,
            NormalizedFingerprint = sourceRecord.NormalizedFingerprint,
            PrimarySourceRecordId = sourceRecord.Id,
            SourceRecords = { sourceRecord },
        };
        db.CompletedActivities.Add(activity);

        if (external.AdditionalMetrics is { Count: > 0 })
        {
            foreach (var metric in external.AdditionalMetrics)
            {
                db.ActivityMetrics.Add(new ActivityMetric
                {
                    CompletedActivity = activity,
                    MetricType = metric.Type,
                    Value = metric.Value,
                    Unit = metric.Unit,
                    Source = dataSource,
                    RecordedAtUtc = clock.UtcNow,
                });
            }
        }

        return activity;
    }

    /// <summary>
    /// Attaches a newly-matched source record to an already-canonical activity instead of
    /// creating a second one, and decides — from the connector policy in effect — whether this
    /// source's values should become the activity's primary (displayed) values. A Primary-ranked
    /// source always takes over; Secondary only takes over if nothing is primary yet;
    /// EnrichmentOnly/FallbackOnly never promote over an existing primary (this is the concrete
    /// mechanism behind "Strava must not create a second canonical activity for something
    /// intervals.icu already reported").
    /// </summary>
    private void AttachAsMergedSource(CompletedActivity activity, ActivitySourceRecord newRecord, ExternalActivity external, ConnectorMode policyMode, ActivityMatchResult match)
    {
        // Explicit Add (not just attaching to activity.SourceRecords) — newRecord has a
        // client-generated, non-default Guid key, so if it were only attached via the navigation
        // on an already-Unchanged parent, EF's change-tracker heuristic would mark it Modified
        // (assuming it already exists) instead of Added, generating an UPDATE against a row that
        // was never inserted.
        newRecord.CompletedActivityId = activity.Id;
        db.ActivitySourceRecords.Add(newRecord);

        var shouldPromote = policyMode switch
        {
            ConnectorMode.Primary => true,
            ConnectorMode.Secondary => activity.PrimarySourceRecordId is null,
            _ => false,
        };

        if (shouldPromote)
        {
            activity.Sport = external.Sport;
            activity.Title = external.Title;
            activity.StartedAtUtc = external.StartedAtUtc;
            activity.DurationSeconds = external.DurationSeconds;
            activity.DistanceMeters = external.DistanceMeters;
            activity.ElevationGainMeters = external.ElevationGainMeters;
            activity.AverageHeartRateBpm = external.AverageHeartRateBpm;
            activity.MaxHeartRateBpm = external.MaxHeartRateBpm;
            activity.AveragePaceSecondsPerKm = external.AveragePaceSecondsPerKm;
            activity.AveragePowerWatts = external.AveragePowerWatts;
            activity.Calories = external.Calories;
            activity.PrimarySourceRecordId = newRecord.Id;
            activity.NormalizedFingerprint = newRecord.NormalizedFingerprint;
        }

        activity.MatchStatus = ActivityMatchStatus.AutoMerged;
        activity.UpdatedAtUtc = clock.UtcNow;

        db.MergeDecisions.Add(new MergeDecision
        {
            AthleteUserId = activity.AthleteUserId,
            SurvivingActivityId = activity.Id,
            AbsorbedSourceRecordId = newRecord.Id,
            ConfidenceScore = match.ConfidenceScore ?? 100,
            Kind = match.DecisionKind ?? MergeDecisionKind.AutoFingerprintExact,
            Outcome = MergeDecisionOutcome.Merged,
            ScoringBreakdownJson = match.ScoringBreakdownJson,
            DecidedAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow,
        });
    }
}
