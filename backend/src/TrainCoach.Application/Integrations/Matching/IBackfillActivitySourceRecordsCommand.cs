namespace TrainCoach.Application.Integrations.Matching;

public record BackfillResult(int ActivitiesScanned, int ActivitiesBackfilled, int ActivitiesSkippedAlreadyBackfilled, int ActivitiesSkippedMultipleSources);

/// <summary>
/// One-off, idempotent data migration: populates <c>PrimarySourceRecordId</c>/<c>NormalizedFingerprint</c>
/// on every pre-existing <c>CompletedActivity</c> that predates this rework. Purely additive — never
/// deletes or merges anything; activities with more than one source record already (shouldn't exist
/// yet pre-rework, but handled defensively) are left for manual review rather than guessed at. Safe
/// to re-run: already-backfilled rows are skipped. See docs/integrations/canonical-data-and-deduplication-plan.md.
/// </summary>
public interface IBackfillActivitySourceRecordsCommand
{
    Task<BackfillResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
