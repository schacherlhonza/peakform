using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

/// <summary>One point of a record's history: an effort that beat every earlier one.</summary>
public record PersonalBestStepDto(DateTime AchievedAtUtc, decimal Value, Guid ActivityId);

/// <summary>Current personal best of one kind (per sport — running power and cycling power are
/// separate records), with the chronological progression that led to it.</summary>
public record PersonalBestDto(
    SportType Sport,
    BestEffortType Type,
    decimal Value,
    Guid ActivityId,
    string? ActivityTitle,
    DateTime AchievedAtUtc,
    bool IsPrecise,
    int EffortCount,
    IReadOnlyList<PersonalBestStepDto> Progression);

/// <summary>A best effort inside one activity, with how it ranks among all of the athlete's
/// efforts of that kind (1 = current personal best).</summary>
public record ActivityBestEffortDto(BestEffortType Type, decimal Value, int StartOffsetSeconds, bool IsPrecise, int Rank, bool IsPersonalBest);

public interface IPersonalBestService
{
    Task<IReadOnlyList<PersonalBestDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityBestEffortDto>> GetForActivityAsync(Guid activityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Personal records derived at read time from <see cref="ActivityBestEffort"/> — never stored, so
/// deleting, merging or purging an activity can't leave a stale record. Lower is better for
/// distance efforts (seconds), higher for power (watts). Manually entered records
/// (<see cref="TrainCoach.Domain.Wellness.PersonalRecord"/>) are a separate, untouched feature.
/// </summary>
public class PersonalBestService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard) : IPersonalBestService
{
    public static bool LowerIsBetter(BestEffortType type) => (int)type < 100;

    public async Task<IReadOnlyList<PersonalBestDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);

        var efforts = await db.ActivityBestEfforts.AsNoTracking()
            .Where(e => e.AthleteUserId == athleteUserId)
            .Select(e => new { e.Sport, e.Type, e.Value, e.CompletedActivityId, e.ActivityStartedAtUtc, e.IsPrecise, e.CompletedActivity.Title })
            .ToListAsync(cancellationToken);

        return efforts
            .GroupBy(e => (e.Sport, e.Type))
            .Select(g =>
            {
                var lower = LowerIsBetter(g.Key.Type);
                var progression = new List<PersonalBestStepDto>();
                decimal? best = null;
                foreach (var e in g.OrderBy(e => e.ActivityStartedAtUtc))
                {
                    if (best is null || (lower ? e.Value < best : e.Value > best))
                    {
                        best = e.Value;
                        progression.Add(new PersonalBestStepDto(e.ActivityStartedAtUtc, e.Value, e.CompletedActivityId));
                    }
                }
                var current = g.First(e => e.CompletedActivityId == progression[^1].ActivityId);
                return new PersonalBestDto(g.Key.Sport, g.Key.Type, current.Value, current.CompletedActivityId, current.Title,
                    current.ActivityStartedAtUtc, current.IsPrecise, g.Count(), progression);
            })
            .OrderBy(p => p.Sport).ThenBy(p => p.Type)
            .ToList();
    }

    public async Task<IReadOnlyList<ActivityBestEffortDto>> GetForActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await db.CompletedActivities.AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new { a.AthleteUserId, a.Sport })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(CompletedActivity), activityId);
        await accessGuard.EnsureAthleteAccessAsync(activity.AthleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);

        var own = await db.ActivityBestEfforts.AsNoTracking()
            .Where(e => e.CompletedActivityId == activityId)
            .ToListAsync(cancellationToken);
        var result = new List<ActivityBestEffortDto>();
        foreach (var e in own.OrderBy(e => e.Type))
        {
            var lower = LowerIsBetter(e.Type);
            var better = await db.ActivityBestEfforts
                .CountAsync(o => o.AthleteUserId == activity.AthleteUserId && o.Sport == e.Sport && o.Type == e.Type
                    && (lower ? o.Value < e.Value : o.Value > e.Value), cancellationToken);
            result.Add(new ActivityBestEffortDto(e.Type, e.Value, e.StartOffsetSeconds, e.IsPrecise, better + 1, better == 0));
        }
        return result;
    }
}
