using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

public class GoalService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock) : IGoalService
{
    public async Task<IReadOnlyList<GoalDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        var goals = await db.Goals
            .Where(g => g.AthleteUserId == athleteUserId)
            .OrderBy(g => g.TargetDate)
            .ToListAsync(cancellationToken);

        return goals.Select(ToDto).ToList();
    }

    public async Task<GoalDto> CreateAsync(Guid callerUserId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var goal = new Goal
        {
            AthleteUserId = request.AthleteUserId,
            SeasonId = request.SeasonId,
            Title = request.Title,
            Description = request.Description,
            TargetDate = request.TargetDate,
            Priority = request.Priority,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(goal);
    }

    public async Task<GoalDto> UpdateAsync(Guid id, UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal", id);

        await accessGuard.EnsureAthleteAccessAsync(goal.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        goal.SeasonId = request.SeasonId;
        goal.Title = request.Title;
        goal.Description = request.Description;
        goal.TargetDate = request.TargetDate;
        goal.Priority = request.Priority;
        goal.IsAchieved = request.IsAchieved;
        goal.AchievedNotes = request.AchievedNotes;
        goal.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(goal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal", id);

        await accessGuard.EnsureAthleteAccessAsync(goal.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static GoalDto ToDto(Goal g) => new(
        g.Id, g.AthleteUserId, g.SeasonId, g.Title, g.Description, g.TargetDate, g.Priority, g.IsAchieved, g.AchievedNotes);
}
