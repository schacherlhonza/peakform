using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Integrations.Matching;

public class ActivityMatchingService(IApplicationDbContext db, IOptions<ActivityMatchingOptions> options) : IActivityMatchingService
{
    public async Task<ActivityMatchResult> FindOrScoreMatchAsync(
        Guid athleteUserId, ExternalActivity incoming, DataSource incomingSource, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        // Level 2 — shared external identity across providers: no live provider (Strava,
        // intervals.icu) exposes a cross-provider id today (see docs/integrations-research.md).
        // Deliberately a no-op reserved extension point, not implemented as a real lookup.

        // Level 3 — FIT file identity: stronger than the fingerprint, decides immediately without
        // going through level-5 scoring. Populated by the Strava archive import; neither live
        // adapter reports it today.
        if (!string.IsNullOrWhiteSpace(incoming.FitFileUuid))
        {
            var fitMatchActivityId = await db.ActivitySourceRecords
                .Where(sr => sr.FitFileUuid == incoming.FitFileUuid && sr.CompletedActivity.AthleteUserId == athleteUserId)
                .Select(sr => sr.CompletedActivityId)
                .FirstOrDefaultAsync(cancellationToken);
            if (fitMatchActivityId != Guid.Empty)
            {
                return new ActivityMatchResult(
                    ActivityMatchOutcome.AutoMerge, fitMatchActivityId, 100,
                    "{\"reason\":\"fit-file-identity\"}", MergeDecisionKind.AutoFitIdentity);
            }
        }

        // Level 4 — deterministic fingerprint, a candidate index only. Widened with a coarser
        // same-sport time-window query to also catch near-fingerprint-miss cases (e.g. one
        // provider rounds the start time slightly differently).
        var fingerprint = ActivityFingerprint.Compute(incoming.Sport, incoming.StartedAtUtc, incoming.DurationSeconds, incoming.DistanceMeters, incoming.DeviceName);
        var windowStart = incoming.StartedAtUtc.AddHours(-2);
        var windowEnd = incoming.StartedAtUtc.AddHours(2);

        var candidates = await db.CompletedActivities
            .Include(a => a.SourceRecords)
            .Where(a => a.AthleteUserId == athleteUserId
                && (a.NormalizedFingerprint == fingerprint
                    || (a.Sport == incoming.Sport && a.StartedAtUtc >= windowStart && a.StartedAtUtc <= windowEnd)))
            .ToListAsync(cancellationToken);

        // A canonical activity already backed by this same source under a different external id
        // is by definition a different real-world activity (e.g. two strength sessions the same
        // afternoon, both on Strava) — one provider never reports the same event twice. Level 1
        // has already ruled out the same external id, so any same-source candidate is excluded.
        candidates.RemoveAll(c => c.SourceRecords.Any(sr => sr.Source == incomingSource));

        if (candidates.Count == 0)
        {
            return new ActivityMatchResult(ActivityMatchOutcome.NoCandidate, null, null, null, null);
        }

        // Level 5 — confidence scoring over the candidate set.
        var scored = candidates
            .Select(c => (Candidate: c, Result: Score(c, FirstDeviceName(c), incoming, opts)))
            .OrderByDescending(x => x.Result.Score)
            .ToList();

        var best = scored[0];
        var breakdownJson = JsonSerializer.Serialize(best.Result.Breakdown);

        // Two candidates tied above the review threshold: never auto-merge an ambiguous case
        // (covers e.g. warm-up + race in the same window, two short same-day activities).
        if (scored.Count > 1 && scored[1].Result.Score == best.Result.Score && best.Result.Score >= opts.ManualReviewThreshold)
        {
            return new ActivityMatchResult(ActivityMatchOutcome.FlagForReview, best.Candidate.Id, best.Result.Score, breakdownJson, null);
        }

        if (best.Result.Score >= opts.AutoMergeThreshold)
        {
            var kind = best.Candidate.NormalizedFingerprint == fingerprint
                ? MergeDecisionKind.AutoFingerprintExact
                : MergeDecisionKind.AutoHighConfidence;
            return new ActivityMatchResult(ActivityMatchOutcome.AutoMerge, best.Candidate.Id, best.Result.Score, breakdownJson, kind);
        }
        if (best.Result.Score >= opts.ManualReviewThreshold)
        {
            return new ActivityMatchResult(ActivityMatchOutcome.FlagForReview, best.Candidate.Id, best.Result.Score, breakdownJson, null);
        }
        return new ActivityMatchResult(ActivityMatchOutcome.TreatAsSeparate, best.Candidate.Id, best.Result.Score, breakdownJson, null);
    }

    private static string? FirstDeviceName(CompletedActivity candidate) =>
        candidate.SourceRecords.Select(sr => sr.DeviceName).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

    internal static (int Score, Dictionary<string, double> Breakdown) Score(
        CompletedActivity candidate, string? candidateDeviceName, ExternalActivity incoming, ActivityMatchingOptions options)
    {
        // Sport mismatch is a hard disqualifier regardless of every other signal — covers the
        // "changed sport type" negative case.
        if (candidate.Sport != incoming.Sport)
        {
            return (0, new Dictionary<string, double> { ["sport"] = 0, ["disqualifiedBySport"] = 1 });
        }

        const double sportWeight = 40, timeWeight = 30, durationWeight = 15, distanceWeight = 15;
        double sportPoints = sportWeight;

        var timeDiffMinutes = Math.Abs((incoming.StartedAtUtc - candidate.StartedAtUtc).TotalMinutes);
        var timePoints = Decay(timeDiffMinutes, options.TimeWindowMinutesForFullScore, options.TimeWindowMinutesMax, timeWeight);

        var maxDuration = Math.Max(incoming.DurationSeconds, candidate.DurationSeconds);
        var durationDiffPct = maxDuration == 0 ? 0 : Math.Abs(incoming.DurationSeconds - candidate.DurationSeconds) / (double)maxDuration;
        var durationPoints = Decay(durationDiffPct, (double)options.DurationTolerancePercentForFullScore, (double)options.DurationToleranceMaxPercent, durationWeight);

        var hasDistanceBoth = incoming.DistanceMeters is > 0 && candidate.DistanceMeters is > 0;
        var noDistanceExactMatch = false;
        double distancePoints = 0;
        double achieved;
        if (hasDistanceBoth)
        {
            var a = (double)incoming.DistanceMeters!.Value;
            var b = (double)candidate.DistanceMeters!.Value;
            var maxDistance = Math.Max(a, b);
            var distanceDiffPct = maxDistance == 0 ? 0 : Math.Abs(a - b) / maxDistance;
            distancePoints = Decay(distanceDiffPct, (double)options.DistanceTolerancePercentForFullScore, (double)options.DistanceToleranceMaxPercent, distanceWeight);
            achieved = sportPoints + timePoints + durationPoints + distancePoints;
        }
        else
        {
            // Exclude (don't zero) the distance component when either side lacks distance, so
            // strength/no-GPS activities aren't unfairly capped — but a hard ceiling still applies
            // below, since sport+time alone must never be enough for a confident auto-merge.
            var raw = sportPoints + timePoints + durationPoints; // out of 85
            achieved = raw * (100.0 / (sportWeight + timeWeight + durationWeight));
            noDistanceExactMatch = timeDiffMinutes <= options.NoDistanceExactStartToleranceMinutes
                && durationDiffPct <= (double)options.NoDistanceExactDurationTolerancePercent;
            if (!noDistanceExactMatch)
            {
                achieved = Math.Min(achieved, options.NoDistanceActivityMaxScore);
            }
        }

        double deviceBonus = 0;
        if (!string.IsNullOrWhiteSpace(incoming.DeviceName) && !string.IsNullOrWhiteSpace(candidateDeviceName)
            && string.Equals(incoming.DeviceName.Trim(), candidateDeviceName!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            deviceBonus = options.DeviceMatchBonus;
        }

        var total = Math.Clamp(achieved + deviceBonus, 0, 100);
        var breakdown = new Dictionary<string, double>
        {
            ["sport"] = sportPoints,
            ["time"] = timePoints,
            ["duration"] = durationPoints,
            ["distance"] = distancePoints,
            ["deviceBonus"] = deviceBonus,
            ["hasDistanceBoth"] = hasDistanceBoth ? 1 : 0,
            ["noDistanceExactMatch"] = noDistanceExactMatch ? 1 : 0,
        };
        return ((int)Math.Round(total), breakdown);
    }

    private static double Decay(double diff, double fullScoreAt, double zeroAt, double weight)
    {
        if (diff <= fullScoreAt) return weight;
        if (diff >= zeroAt) return 0;
        var fraction = 1.0 - (diff - fullScoreAt) / (zeroAt - fullScoreAt);
        return weight * fraction;
    }
}
