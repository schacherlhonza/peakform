using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

public class DuplicateReviewService(IApplicationDbContext db, IDateTimeProvider clock) : IDuplicateReviewService
{
    public async Task<IReadOnlyList<DuplicateCandidateDto>> GetPendingAsync(Guid callerUserId, Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        EnsureSelf(callerUserId, athleteUserId);

        var candidates = await db.DuplicateCandidates
            .Where(c => c.AthleteUserId == athleteUserId && c.Status == DuplicateCandidateStatus.Pending)
            .ToListAsync(cancellationToken);

        var activityIds = candidates.SelectMany(c => new[] { c.ActivityAId, c.ActivityBId }).Distinct().ToList();
        var activities = await db.CompletedActivities
            .Include(a => a.SourceRecords).Include(a => a.AdditionalMetrics)
            .Where(a => activityIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        return candidates
            .Where(c => activities.ContainsKey(c.ActivityAId) && activities.ContainsKey(c.ActivityBId))
            .Select(c => new DuplicateCandidateDto(
                c.Id, c.AthleteUserId,
                ActivityService.ToDto(activities[c.ActivityAId]),
                ActivityService.ToDto(activities[c.ActivityBId]),
                c.ConfidenceScore, c.ScoringBreakdownJson, c.Status))
            .ToList();
    }

    public async Task MergeAsync(Guid callerUserId, Guid duplicateCandidateId, Guid survivingActivityId, CancellationToken cancellationToken = default)
    {
        var candidate = await db.DuplicateCandidates.FirstOrDefaultAsync(c => c.Id == duplicateCandidateId, cancellationToken)
            ?? throw new NotFoundException(nameof(DuplicateCandidate), duplicateCandidateId);
        EnsureSelf(callerUserId, candidate.AthleteUserId);

        if (candidate.Status != DuplicateCandidateStatus.Pending)
        {
            throw new BusinessRuleException("Tento kandidát na duplicitu už byl vyřešen.");
        }
        if (survivingActivityId != candidate.ActivityAId && survivingActivityId != candidate.ActivityBId)
        {
            throw new BusinessRuleException("Ponechaná aktivita musí být jedna ze dvou porovnávaných.");
        }

        var losingActivityId = survivingActivityId == candidate.ActivityAId ? candidate.ActivityBId : candidate.ActivityAId;

        var survivingActivity = await db.CompletedActivities.FirstAsync(a => a.Id == survivingActivityId, cancellationToken);
        var losingActivity = await db.CompletedActivities.Include(a => a.SourceRecords).FirstAsync(a => a.Id == losingActivityId, cancellationToken);

        ApplyMerge(db, candidate, survivingActivity, losingActivity, MergeDecisionKind.ManualConfirmed, ActivityMatchStatus.Confirmed, callerUserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Moves the losing activity's source records onto the survivor, audits each move, soft-deletes
    /// the loser and resolves the candidate. Shared by the manual merge and the automatic
    /// re-evaluation of pending candidates (PendingDuplicateReevaluationJob). Stages only.
    /// </summary>
    internal static void ApplyMerge(
        IApplicationDbContext db, DuplicateCandidate candidate, CompletedActivity surviving, CompletedActivity losing,
        MergeDecisionKind kind, ActivityMatchStatus survivorStatus, Guid? decidedByUserId, DateTime nowUtc)
    {
        foreach (var sourceRecord in losing.SourceRecords.ToList())
        {
            sourceRecord.CompletedActivityId = surviving.Id;
            db.MergeDecisions.Add(new MergeDecision
            {
                AthleteUserId = candidate.AthleteUserId,
                SurvivingActivityId = surviving.Id,
                AbsorbedSourceRecordId = sourceRecord.Id,
                AbsorbedActivityIdBeforeMerge = losing.Id,
                ConfidenceScore = candidate.ConfidenceScore,
                Kind = kind,
                Outcome = MergeDecisionOutcome.Merged,
                ScoringBreakdownJson = candidate.ScoringBreakdownJson,
                DecidedByUserId = decidedByUserId,
                DecidedAtUtc = nowUtc,
                CreatedAtUtc = nowUtc,
            });
        }

        surviving.MatchStatus = survivorStatus;
        surviving.UpdatedAtUtc = nowUtc;

        losing.IsDeleted = true;
        losing.DeletedAtUtc = nowUtc;
        losing.DeletedByUserId = decidedByUserId;

        candidate.Status = surviving.Id == candidate.ActivityAId ? DuplicateCandidateStatus.MergedIntoA : DuplicateCandidateStatus.MergedIntoB;
        candidate.ResolvedByUserId = decidedByUserId;
        candidate.ResolvedAtUtc = nowUtc;
    }

    public async Task DismissAsync(Guid callerUserId, Guid duplicateCandidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await db.DuplicateCandidates.FirstOrDefaultAsync(c => c.Id == duplicateCandidateId, cancellationToken)
            ?? throw new NotFoundException(nameof(DuplicateCandidate), duplicateCandidateId);
        EnsureSelf(callerUserId, candidate.AthleteUserId);

        if (candidate.Status != DuplicateCandidateStatus.Pending)
        {
            throw new BusinessRuleException("Tento kandidát na duplicitu už byl vyřešen.");
        }

        var activityB = await db.CompletedActivities.Include(a => a.SourceRecords).FirstAsync(a => a.Id == candidate.ActivityBId, cancellationToken);
        var absorbedReference = activityB.SourceRecords.FirstOrDefault(sr => sr.Id == activityB.PrimarySourceRecordId) ?? activityB.SourceRecords.First();

        // Nothing is actually absorbed here — AbsorbedSourceRecordId just names which record this
        // "kept separate" decision was about, so the audit trail stays queryable the same way as
        // a real merge.
        db.MergeDecisions.Add(new MergeDecision
        {
            AthleteUserId = candidate.AthleteUserId,
            SurvivingActivityId = candidate.ActivityAId,
            AbsorbedSourceRecordId = absorbedReference.Id,
            AbsorbedActivityIdBeforeMerge = candidate.ActivityBId,
            ConfidenceScore = candidate.ConfidenceScore,
            Kind = MergeDecisionKind.ManualRejectedAsDuplicate,
            Outcome = MergeDecisionOutcome.KeptSeparate,
            ScoringBreakdownJson = candidate.ScoringBreakdownJson,
            DecidedByUserId = callerUserId,
            DecidedAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow,
        });

        candidate.Status = DuplicateCandidateStatus.DismissedAsDistinct;
        candidate.ResolvedByUserId = callerUserId;
        candidate.ResolvedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevertAsync(Guid callerUserId, Guid mergeDecisionId, CancellationToken cancellationToken = default)
    {
        var decision = await db.MergeDecisions.FirstOrDefaultAsync(d => d.Id == mergeDecisionId, cancellationToken)
            ?? throw new NotFoundException(nameof(MergeDecision), mergeDecisionId);
        EnsureSelf(callerUserId, decision.AthleteUserId);

        if (decision.Outcome != MergeDecisionOutcome.Merged)
        {
            throw new BusinessRuleException("Lze vrátit pouze skutečně provedené sloučení.");
        }

        var sourceRecord = await db.ActivitySourceRecords.FirstOrDefaultAsync(sr => sr.Id == decision.AbsorbedSourceRecordId, cancellationToken)
            ?? throw new NotFoundException(nameof(ActivitySourceRecord), decision.AbsorbedSourceRecordId);
        var survivingActivity = await db.CompletedActivities.FirstAsync(a => a.Id == decision.SurvivingActivityId, cancellationToken);

        // Recreate a shell canonical activity for the detached source record. Known limitation:
        // this reconstructs it from the surviving activity's *current* fixed-column values, since
        // a source that never became primary doesn't retain its own copy of those fields
        // separately (only its RawPayloadJson, when retained) — see docs/integrations/canonical-data-and-deduplication-plan.md
        // "Known limitations".
        var revertedActivity = new CompletedActivity
        {
            AthleteUserId = survivingActivity.AthleteUserId,
            Sport = survivingActivity.Sport,
            Title = survivingActivity.Title,
            StartedAtUtc = survivingActivity.StartedAtUtc,
            DurationSeconds = survivingActivity.DurationSeconds,
            DistanceMeters = survivingActivity.DistanceMeters,
            ElevationGainMeters = survivingActivity.ElevationGainMeters,
            AverageHeartRateBpm = survivingActivity.AverageHeartRateBpm,
            MaxHeartRateBpm = survivingActivity.MaxHeartRateBpm,
            AveragePaceSecondsPerKm = survivingActivity.AveragePaceSecondsPerKm,
            AveragePowerWatts = survivingActivity.AveragePowerWatts,
            Calories = survivingActivity.Calories,
            CreatedAtUtc = clock.UtcNow,
            MatchStatus = ActivityMatchStatus.Reverted,
            NormalizedFingerprint = sourceRecord.NormalizedFingerprint,
        };
        db.CompletedActivities.Add(revertedActivity);

        // sourceRecord is already tracked (loaded via FirstOrDefaultAsync above) — just repoint
        // its FK; EF generates an UPDATE for it, not a duplicate INSERT.
        sourceRecord.CompletedActivityId = revertedActivity.Id;
        revertedActivity.PrimarySourceRecordId = sourceRecord.Id;

        if (survivingActivity.PrimarySourceRecordId == sourceRecord.Id)
        {
            var remaining = await db.ActivitySourceRecords
                .Where(sr => sr.CompletedActivityId == survivingActivity.Id && sr.Id != sourceRecord.Id)
                .FirstOrDefaultAsync(cancellationToken);
            survivingActivity.PrimarySourceRecordId = remaining?.Id;
        }
        survivingActivity.UpdatedAtUtc = clock.UtcNow;

        decision.Outcome = MergeDecisionOutcome.Reverted;
        decision.RevertedAtUtc = clock.UtcNow;
        decision.RevertedByUserId = callerUserId;

        // Audit trail requirement: never mutate the original decision's meaning, only record that
        // it was reverted (above) plus a fresh row documenting the revert itself.
        db.MergeDecisions.Add(new MergeDecision
        {
            AthleteUserId = decision.AthleteUserId,
            SurvivingActivityId = revertedActivity.Id,
            AbsorbedSourceRecordId = sourceRecord.Id,
            ConfidenceScore = decision.ConfidenceScore,
            Kind = MergeDecisionKind.ManualConfirmed,
            Outcome = MergeDecisionOutcome.Reverted,
            DecidedByUserId = callerUserId,
            DecidedAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MergeDecisionDto>> GetMergeDecisionsAsync(Guid callerUserId, Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        EnsureSelf(callerUserId, athleteUserId);

        var decisions = await db.MergeDecisions
            .Where(d => d.AthleteUserId == athleteUserId)
            .OrderByDescending(d => d.DecidedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return decisions.Select(d => new MergeDecisionDto(
            d.Id, d.AthleteUserId, d.SurvivingActivityId, d.AbsorbedSourceRecordId, d.ConfidenceScore, d.Kind, d.Outcome, d.DecidedAtUtc, d.RevertedAtUtc)).ToList();
    }

    private static void EnsureSelf(Guid callerUserId, Guid athleteUserId)
    {
        if (callerUserId != athleteUserId)
        {
            throw new ForbiddenAccessException("Duplicity lze řešit pouze pro vlastní aktivity.");
        }
    }
}
