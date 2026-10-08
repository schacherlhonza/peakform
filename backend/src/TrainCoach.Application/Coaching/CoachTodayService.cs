using TrainCoach.Application.Planning;
using TrainCoach.Application.Relationships;
using TrainCoach.Application.Wellness;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Coaching;

/// <summary>
/// One athlete's day at a glance for the coach dashboard. Every part is null/empty when the athlete
/// doesn't share it — the <c>*Shared</c> flags tell "not shared" apart from "nothing there".
/// </summary>
/// <param name="Readiness">Today's readiness; may fall back to an earlier day (<c>Readiness.IsToday</c>).</param>
/// <param name="TodayWorkouts">The day's planned workouts (rest days included) with their matched activity.</param>
/// <param name="UnplannedActivities">Activities of the day that no planned workout took.</param>
/// <param name="WeekPlannedWorkouts">Planned (non-rest) workouts in the athlete's week Monday–Sunday.</param>
public record CoachTodayAthleteDto(
    Guid AthleteUserId,
    string Name,
    string Email,
    bool WellnessShared,
    bool PlanShared,
    bool ActivitiesShared,
    bool HealthFlagsShared,
    ReadinessDto? Readiness,
    IReadOnlyList<WorkoutComparisonDto> TodayWorkouts,
    IReadOnlyList<ActualActivityDto> UnplannedActivities,
    int? WeekPlannedWorkouts,
    int? WeekCompletedWorkouts,
    IReadOnlyList<PainOrHealthFlagDto> ActiveHealthFlags);

public record CoachTodayDto(DateOnly Date, IReadOnlyList<CoachTodayAthleteDto> Athletes);

public interface ICoachTodayService
{
    Task<CoachTodayDto> GetAsync(Guid coachUserId, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Composes the existing per-athlete services (readiness, plan vs actual, health flags) for every active
/// athlete of the coach, so the dashboard makes one request instead of 4–5 per athlete. Each service
/// still runs its own access guard; a part is only requested when the athlete granted that scope.
/// Athletes are processed one by one — the services share one DbContext.
/// </summary>
public class CoachTodayService(
    ICoachAthleteRelationshipService relationships,
    IReadinessService readiness,
    IPlanVsActualService planVsActual,
    IPainOrHealthFlagService healthFlags) : ICoachTodayService
{
    public async Task<CoachTodayDto> GetAsync(Guid coachUserId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var active = (await relationships.GetMyRelationshipsAsync(coachUserId, AppRole.Coach, cancellationToken))
            .Where(r => r.Status == RelationshipStatus.Active)
            .OrderBy(r => r.AthleteName)
            .ToList();
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        var athletes = new List<CoachTodayAthleteDto>();
        foreach (var r in active)
        {
            bool Can(PermissionScope scope) => r.GrantedScopes.Contains(scope);

            var readinessDto = Can(PermissionScope.ViewWellness)
                ? await readiness.GetForAthleteAsync(r.AthleteUserId, date, cancellationToken)
                : null;

            PlanVsActualDto? week = Can(PermissionScope.ViewTrainingPlan)
                ? await planVsActual.GetAsync(r.AthleteUserId, monday, monday.AddDays(6), cancellationToken)
                : null;
            var day = week?.Days.FirstOrDefault(d => d.Date == date);

            var flags = Can(PermissionScope.ViewHealthFlags)
                ? await healthFlags.GetForAthleteAsync(r.AthleteUserId, activeOnly: true, cancellationToken)
                : [];

            athletes.Add(new CoachTodayAthleteDto(
                r.AthleteUserId, r.AthleteName, r.AthleteEmail,
                WellnessShared: readinessDto is not null,
                PlanShared: week is not null,
                ActivitiesShared: week?.ActualAvailable ?? false,
                HealthFlagsShared: Can(PermissionScope.ViewHealthFlags),
                readinessDto,
                day?.Workouts ?? [],
                day?.UnplannedActivities ?? [],
                week?.Totals.PlannedWorkouts,
                week?.ActualAvailable == true ? week.Totals.CompletedWorkouts : null,
                flags));
        }
        return new CoachTodayDto(date, athletes);
    }
}
