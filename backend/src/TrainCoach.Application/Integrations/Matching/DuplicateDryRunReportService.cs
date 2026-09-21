using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Integrations.Matching;

public class DuplicateDryRunReportService(IApplicationDbContext db, IOptions<ActivityMatchingOptions> options, IDateTimeProvider clock) : IDuplicateDryRunReportService
{
    private record CandidateEntry(Guid ActivityAId, Guid ActivityBId, int Score, string Tier);

    public async Task<DuplicateDryRunReportDto> GenerateAsync(Guid? athleteUserId, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        var query = db.CompletedActivities.Include(a => a.SourceRecords).AsQueryable();
        if (athleteUserId is { } id)
        {
            query = query.Where(a => a.AthleteUserId == id);
        }
        var activities = await query.ToListAsync(cancellationToken);

        var candidates = new List<CandidateEntry>();
        var exactCount = 0;
        var highConfidenceCount = 0;
        var uncertainCount = 0;

        foreach (var group in activities.GroupBy(a => a.AthleteUserId))
        {
            var list = group.OrderBy(a => a.StartedAtUtc).ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (Math.Abs((b.StartedAtUtc - a.StartedAtUtc).TotalHours) > 2 && a.NormalizedFingerprint != b.NormalizedFingerprint)
                    {
                        continue;
                    }

                    var deviceA = a.SourceRecords.Select(sr => sr.DeviceName).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
                    var incoming = ToExternalActivity(b);
                    var (score, _) = ActivityMatchingService.Score(a, deviceA, incoming, opts);
                    if (score < opts.ManualReviewThreshold)
                    {
                        continue;
                    }

                    var isExactFingerprint = a.NormalizedFingerprint is not null && a.NormalizedFingerprint == b.NormalizedFingerprint;
                    var tier = score >= opts.AutoMergeThreshold
                        ? (isExactFingerprint ? "Exact" : "HighConfidence")
                        : "Uncertain";

                    switch (tier)
                    {
                        case "Exact": exactCount++; break;
                        case "HighConfidence": highConfidenceCount++; break;
                        default: uncertainCount++; break;
                    }

                    candidates.Add(new CandidateEntry(a.Id, b.Id, score, tier));
                }
            }
        }

        var report = new DuplicateDryRunReport
        {
            AthleteUserId = athleteUserId,
            GeneratedAtUtc = clock.UtcNow,
            TotalActivitiesScanned = activities.Count,
            ExactTierCount = exactCount,
            HighConfidenceTierCount = highConfidenceCount,
            UncertainTierCount = uncertainCount,
            CandidatesJson = JsonSerializer.Serialize(candidates),
        };
        db.DuplicateDryRunReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(report);
    }

    public async Task<DuplicateDryRunReportDto?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var report = await db.DuplicateDryRunReports.OrderByDescending(r => r.GeneratedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return report is null ? null : ToDto(report);
    }

    private static ExternalActivity ToExternalActivity(CompletedActivity a)
    {
        var deviceName = a.SourceRecords.Select(sr => sr.DeviceName).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
        return new ExternalActivity(
            a.Id.ToString(), a.Sport, a.Title, a.StartedAtUtc, a.DurationSeconds, a.DistanceMeters, a.ElevationGainMeters,
            a.AverageHeartRateBpm, a.MaxHeartRateBpm, a.AveragePaceSecondsPerKm, a.AveragePowerWatts, a.Calories,
            DeviceName: deviceName);
    }

    private static DuplicateDryRunReportDto ToDto(DuplicateDryRunReport r) => new(
        r.Id, r.AthleteUserId, r.GeneratedAtUtc, r.TotalActivitiesScanned, r.ExactTierCount, r.HighConfidenceTierCount, r.UncertainTierCount, r.CandidatesJson);
}
