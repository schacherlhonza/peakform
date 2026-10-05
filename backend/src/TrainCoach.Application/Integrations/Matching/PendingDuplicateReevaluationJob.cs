using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Integrations.Matching;

public interface IPendingDuplicateReevaluationJob
{
    /// <summary>Re-scores pending duplicate candidates with the current matcher and merges the ones
    /// that now clear the auto-merge threshold. Null = every athlete. Returns how many were merged.</summary>
    Task<int> RunAsync(Guid? athleteUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Matcher rule changes only apply to activities synced after them — candidates already queued
/// for manual review keep waiting. This pass re-scores those with today's rules and resolves the
/// ones that would now auto-merge exactly as the sync would have (e.g. no-distance pairs with the
/// same start: the rule came after they were flagged). Never merges two records of the same
/// source, and keeps the activity whose primary source the athlete's connector policy ranks
/// higher. Runs once at startup and after every sync.
/// </summary>
public class PendingDuplicateReevaluationJob(
    IApplicationDbContext db,
    IConnectorPolicyService policyService,
    IOptions<ActivityMatchingOptions> options,
    IDateTimeProvider clock,
    ILogger<PendingDuplicateReevaluationJob> logger) : IPendingDuplicateReevaluationJob
{
    public async Task<int> RunAsync(Guid? athleteUserId, CancellationToken cancellationToken = default)
    {
        var candidates = await db.DuplicateCandidates
            .Where(c => c.Status == DuplicateCandidateStatus.Pending && (athleteUserId == null || c.AthleteUserId == athleteUserId))
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return 0;
        }

        var ids = candidates.SelectMany(c => new[] { c.ActivityAId, c.ActivityBId }).Distinct().ToList();
        var activities = await db.CompletedActivities.Include(a => a.SourceRecords)
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var merged = 0;
        foreach (var candidate in candidates)
        {
            if (!activities.TryGetValue(candidate.ActivityAId, out var a) || !activities.TryGetValue(candidate.ActivityBId, out var b)
                || a.IsDeleted || b.IsDeleted)
            {
                continue;
            }
            // Same source on both sides = two different real activities (see ActivityMatchingService).
            if (a.SourceRecords.Any(sa => b.SourceRecords.Any(sb => sb.Source == sa.Source)))
            {
                continue;
            }

            var (score, breakdown) = ActivityMatchingService.Score(a, DeviceOf(a), AsIncoming(b), options.Value);
            if (score < options.Value.AutoMergeThreshold)
            {
                continue;
            }

            var (surviving, losing) = await RankAsync(candidate.AthleteUserId, a, b, cancellationToken);
            candidate.ConfidenceScore = score;
            candidate.ScoringBreakdownJson = System.Text.Json.JsonSerializer.Serialize(breakdown);
            DuplicateReviewService.ApplyMerge(db, candidate, surviving, losing, MergeDecisionKind.AutoHighConfidence, ActivityMatchStatus.AutoMerged, null, clock.UtcNow);
            merged++;
        }

        if (merged > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Přehodnocení duplicit: {Merged} z {Pending} čekajících dvojic sloučeno podle aktuálních pravidel.", merged, candidates.Count);
        }
        return merged;
    }

    private static string? DeviceOf(CompletedActivity a) =>
        a.SourceRecords.Select(sr => sr.DeviceName).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

    private static ExternalActivity AsIncoming(CompletedActivity b) => new(
        ExternalId: string.Empty, Sport: b.Sport, Title: b.Title, StartedAtUtc: b.StartedAtUtc, DurationSeconds: b.DurationSeconds,
        DistanceMeters: b.DistanceMeters, ElevationGainMeters: b.ElevationGainMeters, AverageHeartRateBpm: b.AverageHeartRateBpm,
        MaxHeartRateBpm: b.MaxHeartRateBpm, AveragePaceSecondsPerKm: b.AveragePaceSecondsPerKm, AveragePowerWatts: b.AveragePowerWatts,
        Calories: b.Calories, DeviceName: DeviceOf(b));

    /// <summary>The survivor is the activity whose primary source has the stronger Activities policy
    /// (Primary &gt; Secondary &gt; Fallback/Enrichment) — e.g. intervals.icu over Strava when both are connected.</summary>
    private async Task<(CompletedActivity Surviving, CompletedActivity Losing)> RankAsync(Guid athleteUserId, CompletedActivity a, CompletedActivity b, CancellationToken cancellationToken)
    {
        var rankA = await PolicyRankAsync(athleteUserId, a, cancellationToken);
        var rankB = await PolicyRankAsync(athleteUserId, b, cancellationToken);
        return rankB < rankA ? (b, a) : (a, b);
    }

    private async Task<int> PolicyRankAsync(Guid athleteUserId, CompletedActivity activity, CancellationToken cancellationToken)
    {
        var primary = activity.SourceRecords.FirstOrDefault(sr => sr.Id == activity.PrimarySourceRecordId) ?? activity.SourceRecords.FirstOrDefault();
        IntegrationProviderType? provider = primary?.Source switch
        {
            DataSource.Strava => IntegrationProviderType.Strava,
            DataSource.IntervalsIcu => IntegrationProviderType.IntervalsIcu,
            _ => null,
        };
        if (provider is null)
        {
            return 10;
        }
        var mode = await policyService.GetEffectiveModeAsync(athleteUserId, provider.Value, DataDomain.Activities, cancellationToken);
        return mode switch
        {
            ConnectorMode.Primary => 0,
            ConnectorMode.Secondary => 1,
            _ => 2,
        };
    }
}
