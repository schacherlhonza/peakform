using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

/// <summary>What was actually done: one activity, with its time in heart rate zones.</summary>
/// <param name="ZoneSeconds">Index 0 = zone 1 … zone 7.</param>
public record ActualActivityDto(
    Guid ActivityId, string? Title, SportType Sport, DateTime StartedAtUtc, int DurationSeconds, decimal? DistanceMeters,
    IReadOnlyList<int> ZoneSeconds, int BelowZonesSeconds, decimal? TrainingLoad);

/// <summary>One planned workout next to the activity that fulfilled it (if any).</summary>
/// <param name="PlannedZoneSeconds">From the workout's segments that target a heart rate zone (duration × repeats); index 0 = zone 1.</param>
/// <param name="PlannedUnspecifiedSeconds">Planned time with no zone target (free, pace, RPE, power targets).</param>
/// <param name="SportMismatch">The paired activity is another sport (no same-sport activity that day).</param>
public record WorkoutComparisonDto(
    Guid WorkoutId, string Title, SportType Sport, bool IsRestDay,
    int? PlannedDurationSeconds, decimal? PlannedDistanceMeters,
    IReadOnlyList<int> PlannedZoneSeconds, int PlannedUnspecifiedSeconds,
    ActualActivityDto? Actual, bool SportMismatch, int? DurationCompliancePercent, int? DistanceCompliancePercent);

public record PlanVsActualDayDto(DateOnly Date, IReadOnlyList<WorkoutComparisonDto> Workouts, IReadOnlyList<ActualActivityDto> UnplannedActivities);

public record PlanVsActualTotalsDto(
    int PlannedDurationSeconds, decimal PlannedDistanceMeters, int ActualDurationSeconds, decimal ActualDistanceMeters,
    IReadOnlyList<int> PlannedZoneSeconds, int PlannedUnspecifiedSeconds, IReadOnlyList<int> ActualZoneSeconds, int ActualBelowZonesSeconds,
    int PlannedWorkouts, int CompletedWorkouts);

/// <param name="ActualAvailable">False when the caller (a coach) may see the plan but not the activities.</param>
public record PlanVsActualDto(DateOnly From, DateOnly To, bool ActualAvailable, IReadOnlyList<PlanVsActualDayDto> Days, PlanVsActualTotalsDto Totals);

public interface IPlanVsActualService
{
    Task<PlanVsActualDto> GetAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// The calendar's plan-vs-actual comparison. Each planned workout gets at most one activity:
/// one explicitly linked to it (<c>PlannedWorkoutId</c>) first, else the longest same-sport
/// activity of that day (athlete's time zone) not yet taken, else — once every workout of the day
/// had its chance at its own sport — the longest remaining activity, flagged as another sport
/// (a strength session planned as a run still counts as done). Everything else that day is
/// "unplanned". Time in zones comes from the PeakForm zone metrics (HrZoneRecomputeJob); planned
/// zones from the workout's segments. See docs/planning/plan-vs-actual.md.
/// </summary>
public class PlanVsActualService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard) : IPlanVsActualService
{
    private const int MaxRangeDays = 62;
    private const int ZoneCount = 7;

    private static readonly ActivityMetricType[] ZoneMetrics =
    [
        ActivityMetricType.TimeInHrZone1, ActivityMetricType.TimeInHrZone2, ActivityMetricType.TimeInHrZone3,
        ActivityMetricType.TimeInHrZone4, ActivityMetricType.TimeInHrZone5, ActivityMetricType.TimeInHrZone6,
        ActivityMetricType.TimeInHrZone7,
    ];

    public async Task<PlanVsActualDto> GetAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);
        if (to < from || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw new BusinessRuleException($"Rozsah musí být 1–{MaxRangeDays} dní.");
        }

        var workouts = await db.PlannedWorkouts.AsNoTracking()
            .Include(w => w.Segments)
            .Where(w => w.TrainingWeek.TrainingPlan.AthleteUserId == athleteUserId && !w.TrainingWeek.TrainingPlan.IsDeleted
                && w.Date >= from && w.Date <= to)
            .OrderBy(w => w.Date)
            .ToListAsync(cancellationToken);

        var zoneNumberById = await db.HeartRateZones.AsNoTracking()
            .Where(z => z.AthleteUserId == athleteUserId)
            .ToDictionaryAsync(z => z.Id, z => z.ZoneNumber, cancellationToken);

        var actualAvailable = await accessGuard.HasAthleteAccessAsync(athleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);
        var activities = actualAvailable ? await LoadActivitiesAsync(athleteUserId, from, to, cancellationToken) : [];

        var days = new List<PlanVsActualDayDto>();
        var taken = new HashSet<Guid>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayActivities = activities.Where(a => a.Date == date).ToList();
            var dayWorkouts = workouts.Where(w => w.Date == date && !w.IsRestDay).ToList();
            var matches = new Dictionary<Guid, LoadedActivity>();
            LoadedActivity? Take(Func<LoadedActivity, bool> predicate)
            {
                var found = dayActivities.Where(a => !taken.Contains(a.Dto.ActivityId) && predicate(a))
                    .OrderByDescending(a => a.Dto.DurationSeconds).FirstOrDefault();
                if (found is not null)
                {
                    taken.Add(found.Dto.ActivityId);
                }
                return found;
            }
            // Three passes so a cross-sport fallback never steals an activity that matches another workout's sport.
            foreach (var w in dayWorkouts)
            {
                if (Take(a => a.PlannedWorkoutId == w.Id) is { } explicitMatch) matches[w.Id] = explicitMatch;
            }
            foreach (var w in dayWorkouts.Where(w => !matches.ContainsKey(w.Id)))
            {
                if (Take(a => a.Dto.Sport == w.Sport) is { } sameSport) matches[w.Id] = sameSport;
            }
            var mismatched = new HashSet<Guid>();
            foreach (var w in dayWorkouts.Where(w => !matches.ContainsKey(w.Id)))
            {
                if (Take(_ => true) is { } other)
                {
                    matches[w.Id] = other;
                    mismatched.Add(w.Id);
                }
            }

            var comparisons = new List<WorkoutComparisonDto>();
            foreach (var w in workouts.Where(w => w.Date == date))
            {
                var match = matches.GetValueOrDefault(w.Id);
                var (plannedZones, unspecified) = PlannedZones(w, zoneNumberById);
                comparisons.Add(new WorkoutComparisonDto(
                    w.Id, w.Title, w.Sport, w.IsRestDay, w.PlannedDurationSeconds, w.PlannedDistanceMeters, plannedZones, unspecified,
                    match?.Dto, mismatched.Contains(w.Id),
                    Percent(match?.Dto.DurationSeconds, w.PlannedDurationSeconds),
                    Percent(match?.Dto.DistanceMeters, w.PlannedDistanceMeters)));
            }
            var unplanned = dayActivities.Where(a => !taken.Contains(a.Dto.ActivityId)).Select(a => a.Dto).ToList();
            days.Add(new PlanVsActualDayDto(date, comparisons, unplanned));
        }

        var allComparisons = days.SelectMany(d => d.Workouts).ToList();
        var totals = new PlanVsActualTotalsDto(
            allComparisons.Sum(c => c.PlannedDurationSeconds ?? 0),
            allComparisons.Sum(c => c.PlannedDistanceMeters ?? 0),
            activities.Sum(a => a.Dto.DurationSeconds),
            activities.Sum(a => a.Dto.DistanceMeters ?? 0),
            Enumerable.Range(0, ZoneCount).Select(i => allComparisons.Sum(c => c.PlannedZoneSeconds[i])).ToList(),
            allComparisons.Sum(c => c.PlannedUnspecifiedSeconds),
            Enumerable.Range(0, ZoneCount).Select(i => activities.Sum(a => a.Dto.ZoneSeconds[i])).ToList(),
            activities.Sum(a => a.Dto.BelowZonesSeconds),
            allComparisons.Count(c => !c.IsRestDay),
            allComparisons.Count(c => !c.IsRestDay && c.Actual is not null));

        return new PlanVsActualDto(from, to, actualAvailable, days, totals);
    }

    private sealed record LoadedActivity(DateOnly Date, Guid? PlannedWorkoutId, ActualActivityDto Dto);

    private async Task<List<LoadedActivity>> LoadActivitiesAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var zone = AthleteLocalDates.ResolveTimeZone(await db.UserProfiles
            .Where(p => p.Id == athleteUserId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(cancellationToken));
        var (fromUtc, toUtc) = AthleteLocalDates.ToUtcRange(from, to, zone);

        var rows = await db.CompletedActivities.AsNoTracking()
            .Include(a => a.AdditionalMetrics)
            .Where(a => a.AthleteUserId == athleteUserId && a.StartedAtUtc >= fromUtc && a.StartedAtUtc < toUtc)
            .OrderBy(a => a.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(a =>
        {
            decimal Metric(ActivityMetricType type) => a.AdditionalMetrics.Where(m => m.MetricType == type).Sum(m => m.Value);
            var load = a.AdditionalMetrics
                .Where(m => m.MetricType == ActivityMetricType.TrainingLoad)
                .OrderBy(m => m.Source == DataSource.PeakForm ? 0 : 1)
                .Select(m => (decimal?)m.Value)
                .FirstOrDefault();
            var dto = new ActualActivityDto(
                a.Id, a.Title, a.Sport, a.StartedAtUtc, a.DurationSeconds, a.DistanceMeters,
                ZoneMetrics.Select(t => (int)Metric(t)).ToList(), (int)Metric(ActivityMetricType.TimeBelowHrZones), load);
            return new LoadedActivity(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(a.StartedAtUtc, zone)), a.PlannedWorkoutId, dto);
        }).ToList();
    }

    internal static (IReadOnlyList<int> Zones, int Unspecified) PlannedZones(PlannedWorkout w, IReadOnlyDictionary<Guid, int> zoneNumberById)
    {
        var zones = new int[ZoneCount];
        var unspecified = 0;
        var blockRepeats = w.Segments.Where(s => s.Type == WorkoutSegmentType.Repeat).ToDictionary(s => s.Id, s => Math.Max(1, s.RepeatCount ?? 1));
        foreach (var s in w.Segments)
        {
            if (s.DurationSeconds is not { } seconds || seconds <= 0)
            {
                continue; // distance-only segments (and repeat blocks themselves) have no planned time to compare
            }
            var parentRepeats = s.ParentSegmentId is { } parentId ? blockRepeats.GetValueOrDefault(parentId, 1) : 1;
            var total = seconds * Math.Max(1, s.RepeatCount ?? 1) * parentRepeats;
            var number = s.TargetHeartRateZoneNumber
                ?? (s.TargetHeartRateZoneId is { } zoneId ? zoneNumberById.GetValueOrDefault(zoneId) : 0);
            if (s.IntensityTargetType == IntensityTargetType.HeartRateZone && number is >= 1 and <= ZoneCount)
            {
                zones[number - 1] += total;
            }
            else
            {
                unspecified += total;
            }
        }
        return (zones, unspecified);
    }

    private static int? Percent(decimal? actual, decimal? planned) =>
        actual is { } a && planned is > 0 ? (int)Math.Round(a / planned.Value * 100) : null;

    private static int? Percent(int? actual, int? planned) => Percent((decimal?)actual, (decimal?)planned);
}
